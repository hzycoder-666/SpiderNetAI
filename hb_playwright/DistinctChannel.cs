using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace hb_playwright
{
    /// <summary>
    /// 基于 Channel 的去重队列：负责去重、排队和元数据存储/释放
    /// 可在生产端使用 TryWrite/EnqueueAsync 写入，消费端使用 Reader 或 TryRead 读取。
    /// 完成处理后请调用 Complete(key) 以释放去重记录，或在不再写入时调用 CompleteWriter() 关闭写入端。
    /// </summary>
    /// <typeparam name="TMeta"></typeparam>
    internal sealed class DistinctChannel<TMeta>
    {
        private readonly Channel<(string Key, TMeta Meta)> _channel;
        private readonly ConcurrentDictionary<string, byte> _set = new();
        private readonly ConcurrentDictionary<string, TMeta> _meta = new();
        private readonly Action<string, TMeta>? _onEnqueue;

        public DistinctChannel(Action<string, TMeta>? onEnqueue = null, bool singleReader = false, bool singleWriter = false)
        {
            _onEnqueue = onEnqueue;
            var options = new UnboundedChannelOptions { SingleReader = singleReader, SingleWriter = singleWriter };
            _channel = Channel.CreateUnbounded<(string, TMeta)>(options);
        }

        /// <summary>
        /// 同步尝试写入（去重）：如果 key 已存在返回 false。
        /// </summary>
        public bool TryWrite(string key, TMeta meta)
        {
            if (_set.TryAdd(key, 0))
            {
                _meta[key] = meta;
                _onEnqueue?.Invoke(key, meta);
                // 对于无界 Channel，TryWrite 通常成功；若失败则回退去重标记
                if (!_channel.Writer.TryWrite((key, meta)))
                {
                    _meta.TryRemove(key, out _);
                    _set.TryRemove(key, out _);
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 异步写入（去重）：如果 key 已存在立即返回 false，否则将项写入 Channel。
        /// </summary>
        public async Task<bool> EnqueueAsync(string key, TMeta meta, CancellationToken cancellationToken = default)
        {
            if (!_set.TryAdd(key, 0))
                return false;

            _meta[key] = meta;
            _onEnqueue?.Invoke(key, meta);
            try
            {
                await _channel.Writer.WriteAsync((key, meta), cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // 写入失败则回退去重记录
                _meta.TryRemove(key, out _);
                _set.TryRemove(key, out _);
                throw;
            }
        }

        /// <summary>
        /// 读取端的 ChannelReader，消费者可以使用它的 ReadAllAsync/ReadAsync/TryRead。
        /// </summary>
        public ChannelReader<(string Key, TMeta Meta)> Reader => _channel.Reader;

        /// <summary>
        /// 尝试同步读取一个元素，若成功返回 true 并输出 key/meta。
        /// </summary>
        public bool TryRead(out string? key, out TMeta meta)
        {
            if (_channel.Reader.TryRead(out var item))
            {
                key = item.Key;
                meta = item.Meta;
                return true;
            }
            key = null;
            meta = default!;
            return false;
        }

        /// <summary>
        /// 标记完成：从去重集合和元数据字典中移除（允许重试或释放内存）。
        /// </summary>
        public bool Complete(string key)
        {
            _meta.TryRemove(key, out _);
            return _set.TryRemove(key, out _);
        }

        /// <summary>
        /// 关闭写入端，消费者通过 Reader 会在读尽后结束迭代。
        /// </summary>
        public void CompleteWriter()
        {
            _channel.Writer.Complete();
        }

        public bool Contains(string key) => _set.ContainsKey(key);

        public int Count => _set.Count;

        /// <summary>
        /// 清空 Channel（尝试读取并丢弃所有待处理项）并清理去重集合/元数据。
        /// </summary>
        public void Clear()
        {
            while (_channel.Reader.TryRead(out _)) { }
            _set.Clear();
            _meta.Clear();
        }
    }
}
