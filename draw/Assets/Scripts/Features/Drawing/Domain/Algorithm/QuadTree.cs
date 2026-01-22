using System.Collections.Generic;
using UnityEngine;

namespace Features.Drawing.Domain.Algorithm
{
    public class QuadTree<T>
    {
        private readonly Rect _bounds;
        private readonly int _capacity;
        private readonly int _maxDepth;
        private readonly int _depth;
        
        private readonly List<Entry> _items;
        private QuadTree<T>[] _children;
        private bool _isDivided;

        private struct Entry
        {
            public T Item;
            public Rect Bounds;
        }

        public QuadTree(Rect bounds, int capacity = 10, int maxDepth = 5, int currentDepth = 0)
        {
            _bounds = bounds;
            _capacity = capacity;
            _maxDepth = maxDepth;
            _depth = currentDepth;
            _items = new List<Entry>(capacity);
            _isDivided = false;
        }

        public bool Insert(T item, Rect itemBounds)
        {
            if (!_bounds.Overlaps(itemBounds))
            {
                return false;
            }

            if (_items.Count < _capacity || _depth >= _maxDepth)
            {
                _items.Add(new Entry { Item = item, Bounds = itemBounds });
                return true;
            }

            if (!_isDivided)
            {
                Subdivide();
            }

            // Try to insert into children
            bool insertedIntoChild = false;
            foreach (var child in _children)
            {
                if (child.Insert(item, itemBounds))
                {
                    insertedIntoChild = true;
                }
            }
            
            // If the item overlaps multiple children, we might have inserted it into multiple.
            // Or, strictly speaking, a QuadTree usually stores items in the smallest node that fully contains them.
            // But for simple collision querying, storing in all overlapping leaves (or nodes) is one strategy.
            // Alternatively, store in this node if it doesn't fit fully into any child?
            // Let's go with: Store in this node if it overlaps multiple children? 
            // No, simpler for point/small rect queries: Push down to all overlapping children.
            // If we do that, we get duplicates in Query. We can handle duplicates using a HashSet in Query.
            
            return insertedIntoChild;
        }

        private void Subdivide()
        {
            float x = _bounds.x;
            float y = _bounds.y;
            float w = _bounds.width / 2f;
            float h = _bounds.height / 2f;

            _children = new QuadTree<T>[4];
            _children[0] = new QuadTree<T>(new Rect(x + w, y, w, h), _capacity, _maxDepth, _depth + 1);     // SE
            _children[1] = new QuadTree<T>(new Rect(x, y, w, h), _capacity, _maxDepth, _depth + 1);         // SW
            _children[2] = new QuadTree<T>(new Rect(x, y + h, w, h), _capacity, _maxDepth, _depth + 1);     // NW
            _children[3] = new QuadTree<T>(new Rect(x + w, y + h, w, h), _capacity, _maxDepth, _depth + 1); // NE

            _isDivided = true;
            
            // Re-distribute existing items?
            // If we push items down, we should move them. 
            // But if an item overlaps multiple children, it might need to stay here or be duplicated.
            // Let's keep it simple: Items stay where they are inserted initially? 
            // No, standard QuadTree behavior:
            // If we split, we should try to move existing items to children to keep the tree balanced.
            
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var entry = _items[i];
                bool moved = false;
                foreach (var child in _children)
                {
                    if (child.Insert(entry.Item, entry.Bounds))
                    {
                        moved = true;
                    }
                }
                
                // If moved to at least one child, remove from here?
                // Only if we want leaf-only storage (or deep storage).
                // If an item spans the center line, it overlaps multiple children.
                // Strategy: Store in the node that FULLY contains it?
                // Strategy 2: Store in all overlapping leaves. (Easier for query, costlier for insert/memory).
                // Strategy 3: Loose QuadTree.
                
                // Let's use Strategy 2 (Store in all overlapping nodes) for robustness with strokes.
                // So if we successfully inserted into children, we remove from here.
                if (moved)
                {
                    _items.RemoveAt(i);
                }
            }
        }

        public void Query(Rect area, HashSet<T> results)
        {
            if (!_bounds.Overlaps(area))
            {
                return;
            }

            foreach (var entry in _items)
            {
                if (entry.Bounds.Overlaps(area))
                {
                    results.Add(entry.Item);
                }
            }

            if (_isDivided)
            {
                foreach (var child in _children)
                {
                    child.Query(area, results);
                }
            }
        }
        
        public void Clear()
        {
            _items.Clear();
            _children = null;
            _isDivided = false;
        }
    }
}
