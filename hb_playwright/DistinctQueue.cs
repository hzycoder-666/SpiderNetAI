using System;
using System.Collections.Concurrent;

namespace hb_playwright
{
    /// <summary>
    /// 封装类型：负责去重、排队和元数据存储/释放
    /// </summary>
    /// <typeparam name="TMeta"></typeparam>
    internal sealed class DistinctQueue<TMeta>
    {
        private readonly ConcurrentQueue<string> _queue = new();
        private readonly ConcurrentDictionary<string, byte> _set = new();
        private readonly ConcurrentDictionary<string, TMeta> _meta = new();
        private readonly Action<string, TMeta>? _onEnqueue;

        public DistinctQueue(Action<string, TMeta>? onEnqueue = null)
        {
            _onEnqueue = onEnqueue;
        }

        // 只有第一次成功加入时才入队并记录元数据（原子操作）
        public bool TryEnqueue(string key, TMeta meta)
        {
            if (_set.TryAdd(key, 0))
            {
                _queue.Enqueue(key);
                _meta[key] = meta; // upsert
                _onEnqueue?.Invoke(key, meta);
                return true;
            }
            return false;
        }

        // 出队并返回对应的元数据（不在此处移除去重标记）
        public bool TryDequeue(out string? key, out TMeta meta)
        {
            if (_queue.TryDequeue(out key) && key != null)
            {
                if (!_meta.TryGetValue(key, out meta!))
                {
                    meta = default!; // 元数据丢失时返回默认（调用方可根据业务调整）
                }
                return true;
            }

            key = null;
            meta = default!;
            return false;
        }

        // 标记完成：从去重集合和元数据字典中移除（用于允许重试或释放内存）
        public bool Complete(string key)
        {
            _meta.TryRemove(key, out _);
            return _set.TryRemove(key, out _);
        }

        public bool Contains(string key) => _set.ContainsKey(key);

        public int Count => _set.Count;

        public void Clear()
        {
            while (_queue.TryDequeue(out _)) { }
            _set.Clear();
            _meta.Clear();
        }
    }
}