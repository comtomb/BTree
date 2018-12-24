﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace TomB.Util.Collections
{
    public class BTreeSortedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        #region Nodes
        static long nextNodeId = 1;
        [DebuggerDisplayAttribute("ID = {ID} Num={NumItems}")]
        private abstract class BTreeNode
        {
            public long ID;
            public abstract bool IsLeaf {get;}
            public abstract bool IsInner { get; }
            protected readonly BTreeSortedDictionary<TKey, TValue> tree;
            protected readonly IComparer<TKey> comparer;
            protected readonly int degree;
            public readonly KeyValuePair<TKey, TValue>[] items;
            public int NumItems { get; set; }
            public int MaxItems
            {
                get
                {
                    return degree * 2 - 1;
                }
            }
            public int MinItems
            {
                get
                {
                    return degree - 1;
                }
            }


            protected BTreeNode(int degree, BTreeSortedDictionary<TKey,TValue> tree, IComparer<TKey> comparer)
            {
                this.tree = tree;
                this.comparer = comparer;
                this.degree = degree;
                items = new KeyValuePair<TKey, TValue>[degree * 2 - 1];
                ID = nextNodeId++;
            }



            public void SetItem( int idx, KeyValuePair<TKey,TValue> item)
            {
                throw new NotImplementedException();
            }
            public KeyValuePair<TKey,TValue> GetItem(int idx)
            {
                return items[idx];
            }
            public int FindItem(TKey k)
            {
                // classic binary search in a sorted array
                int low = 0;
                int hi = NumItems - 1;
                int mid = 0;
                while (low <= hi)
                {
                    mid = (low + hi) >> 1;
                    int cmp = comparer.Compare(k, items[mid].Key);
                    if (cmp == 0)
                        return mid;
                    else
                        if (cmp < 0)
                        hi = mid - 1;
                    else
                        low = ++mid;
                }
                return -mid - 1;
            }

            public abstract BTreeNode Split(out KeyValuePair<TKey, TValue> splitItem);

        }

        private class BTreeInnerNode : BTreeNode
        {
            public override bool IsLeaf
            {
                get
                {
                    return false;
                }
            }
            public override bool IsInner
            {
                get
                {
                    return true;
                }
            }
            public int NumChildren
            {
                get
                {
                    return NumItems + 1;
                }
            }
            public BTreeNode[] children;
            public BTreeInnerNode(int degree, BTreeSortedDictionary<TKey, TValue> tree, IComparer<TKey> comparer)
                : base(degree,tree, comparer)
            {
                children = new BTreeNode[degree * 2];
            }
            public override BTreeNode Split(out KeyValuePair<TKey, TValue> splitItem)
            {
                var newRight = new BTreeInnerNode(degree, tree, comparer);
                int mid = MaxItems / 2;
                int len = MaxItems / 2;
                splitItem = items[mid];

                Array.Copy(items, mid + 1, newRight.items, 0, len);
                Array.Copy(children, mid + 1, newRight.children, 0, len + 1);
                newRight.NumItems = len;
                NumItems = len;
                for (int i = 0; i < len; i++)
                {
                    items[mid + 1 + i] = default(KeyValuePair<TKey, TValue>);
                }
                for (int i = 0; i <=len; i++)
                {
                    children[mid + 1 + i] = null;
                }
                return newRight;
            }
            public void InsertItemWithRightChild(int idx, KeyValuePair<TKey, TValue> item, BTreeNode rightChild)
            {
                Array.Copy(items, idx, items, idx + 1, NumItems - idx);
                items[idx] = item;
                Array.Copy(children, idx+1, children, idx + 2, NumItems + 1 - idx-1);
                children[idx+1] = rightChild;
                NumItems++;
            }
            public void InsertItemWithLeftChild(int idx, KeyValuePair<TKey, TValue> item, BTreeNode leftChild)
            {
                Array.Copy(items, idx, items, idx + 1, NumItems - idx);
                items[idx] = item;
                Array.Copy(children, idx, children, idx + 1, NumItems + 1 - idx);
                children[idx] = leftChild;
                NumItems++;
            }


        }
        private class BTreeLeafNode : BTreeNode
        {
            public override bool IsLeaf
            {
                get
                {
                    return true;
                }
            }
            public override bool IsInner
            {
                get
                {
                    return false;
                }
            }
            public BTreeLeafNode(int degree,BTreeSortedDictionary<TKey, TValue> tree, IComparer<TKey> comparer)
                :base(degree,tree,comparer)
            {

            }
            public void InsertItem(int idx, KeyValuePair<TKey, TValue> item)
            {
                Array.Copy(items, idx, items, idx + 1, NumItems - idx);
                items[idx] = item;
                NumItems++;
            }
            public override BTreeNode Split(out KeyValuePair<TKey, TValue> splitItem)
            {
                var newRight = new BTreeLeafNode(degree, tree, comparer);
                int mid = MaxItems / 2;
                int len = MaxItems / 2;
                splitItem = items[mid];

                Array.Copy(items, mid + 1, newRight.items, 0, len);
                newRight.NumItems = len;
                NumItems = len;
                for (int i = 0; i < len; i++)
                    items[mid + 1 + i] = default(KeyValuePair<TKey, TValue>);
                return newRight;
            }

        }
        #endregion


        #region Enumerators & Collections
        private class ValueCollection : ICollection<TValue>
        {
            public int Count
            {
                get
                {
                    return tree.Count;
                }
            }

            public bool IsReadOnly
            {
                get
                {
                    return true;
                }
            }
            private readonly BTreeSortedDictionary<TKey, TValue> tree;
            public ValueCollection( BTreeSortedDictionary<TKey,TValue> tree)
            {
                this.tree = tree;
            }

            public void Add(TValue item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(TValue item)
            {
                return tree.ContainsValue(item);
            }

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                foreach (var kv in tree)
                    array[arrayIndex++] = kv.Value;
            }

            public IEnumerator<TValue> GetEnumerator()
            {
                return new ValueEnumerator(tree);
            }

            public bool Remove(TValue item)
            {
                throw new NotSupportedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ValueEnumerator(tree);
            }
        }
        private class KeyCollection : ICollection<TKey>
        {
            public int Count
            {
                get
                {
                    return tree.Count;
                }
            }

            public bool IsReadOnly
            {
                get
                {
                    return true;
                }
            }
            private readonly BTreeSortedDictionary<TKey, TValue> tree;
            public KeyCollection(BTreeSortedDictionary<TKey, TValue> tree)
            {
                this.tree = tree;
            }

            public void Add(TKey item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(TKey item)
            {
                return tree.ContainsKey(item);
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                foreach (var kv in tree)
                    array[arrayIndex++] = kv.Key;
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                return new KeyEnumerator(tree);
            }

            public bool Remove(TKey item)
            {
                throw new NotSupportedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new KeyEnumerator(tree);
            }
        }
        private class KeyValueEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    // TODO Current
                    throw new NotImplementedException();
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    // TODO Current
                    throw new NotImplementedException();
                }
            }
            private readonly BTreeSortedDictionary<TKey, TValue> tree;
            public KeyValueEnumerator(BTreeSortedDictionary<TKey, TValue> tree)
            {
                this.tree = tree;
            }

            public void Dispose()
            {
                
            }

            public bool MoveNext()
            {
                // TODO MoveNext
                throw new NotImplementedException();
            }

            public void Reset()
            {
                // TODO Reset
                throw new NotImplementedException();
            }
        }
        private class ValueEnumerator : IEnumerator<TValue>
        {
            public TValue Current
            {
                get
                {
                    return kvEnum.Current.Value;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return kvEnum.Current.Value;
                }
            }
            private readonly KeyValueEnumerator kvEnum;
            public ValueEnumerator(BTreeSortedDictionary<TKey, TValue> tree)
            {
                kvEnum = new KeyValueEnumerator(tree);
            }

            public void Dispose()
            {
                
            }

            public bool MoveNext()
            {
                return kvEnum.MoveNext();
            }

            public void Reset()
            {
                kvEnum.Reset();
            }
        }
        private class KeyEnumerator : IEnumerator<TKey>
        {
            public TKey Current
            {
                get
                {
                    return kvEnum.Current.Key;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return kvEnum.Current.Value;
                }
            }
            private readonly KeyValueEnumerator kvEnum;
            public KeyEnumerator( BTreeSortedDictionary<TKey,TValue> tree )
            {
                kvEnum = new KeyValueEnumerator(tree);
            }
            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                return kvEnum.MoveNext();
            }

            public void Reset()
            {
                kvEnum.Reset();
            }
        }

        #endregion


        #region Properties&Co
        private readonly int degree;
        private readonly IComparer<TKey> comparer;
        private BTreeNode root;
        private int count;
        private int modCount;
        private const int ModCountMask = 0xfffffff;
        private int maxItems;
        private int minItems;
        #endregion

        #region Constructors
        public BTreeSortedDictionary(int degree,IComparer<TKey> comparer=null)
            : base()
        {
            this.degree = degree;
            maxItems = degree * 2 - 1;
            minItems = degree - 1;
            this.comparer = comparer ?? Comparer<TKey>.Default;
            root = new BTreeLeafNode(degree, this, this.comparer);
        }
        #endregion


        #region IDictionary
        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (!TryGetValue(key, out value))
                    throw new KeyNotFoundException();
                return value;
            }
            set
            {
                int idx;
                var node = FindNode(key, out idx);
                if (node == null)
                    throw new KeyNotFoundException();
                node.SetItem(idx,new KeyValuePair<TKey,TValue>(key, value));
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return new KeyCollection(this);
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                return new ValueCollection(this);
            }
        }

        public int Count
        {
            get
            {
                return count;
            }

        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }


        public void Add(TKey key, TValue value)
        {
            AddNew(new KeyValuePair<TKey,TValue>(key, value));
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            if (!AddNew(item))
                throw new ArgumentException();
        }

        public void Clear()
        {
            root = new BTreeLeafNode(degree, this, comparer);
            count = 0;
            modCount = (modCount + 1) & ModCountMask;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue v;
            if (!TryGetValue(item.Key, out v))
                return true;
            return v.Equals(item.Value);
        }

        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out TValue v);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var kv in this)
                array[arrayIndex++] = kv;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new KeyValueEnumerator(this);
        }

        public bool Remove(TKey key)
        {
            return Remove(new KeyValuePair<TKey,TValue>(key,default(TValue)),false);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item, true);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int idx;
            var node = FindNode(key, out idx);
            if (node == null)
            {
                value = default(TValue);
                return false;
            }
            value = node.GetItem(idx).Value;
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new KeyValueEnumerator(this);
        }
        #endregion

        #region Core
        private bool AddNew(KeyValuePair<TKey,TValue> item )
        {
            if( root.NumItems==maxItems)
            {
                KeyValuePair<TKey, TValue> splitItem;
                var newRight = root.Split(out splitItem);
                var newRoot = new BTreeInnerNode(degree, this, comparer);
                newRoot.InsertItemWithRightChild(0, splitItem, newRight);
                newRoot.children[0] = root;
                root = newRoot;
            }
            var x = root;
            while(true )
            {
                int ip = x.FindItem(item.Key);
                if (ip >= 0)
                    return false;
                ip = -ip - 1;
                if(x.IsLeaf)
                {
                    var xLeaf = (BTreeLeafNode)x;
                    xLeaf.InsertItem(ip, item);
                    count++;
                    modCount=(modCount+1)&ModCountMask;
                    return true;
                }
                else
                {
                    var xInner = (BTreeInnerNode)x;
                    var y = xInner.children[ip];
                    if (y.NumItems == maxItems)
                    {
                        KeyValuePair<TKey, TValue> splitItem;
                        var newRight = y.Split(out splitItem);
                        xInner.InsertItemWithRightChild(ip, splitItem, newRight);
                        x = (comparer.Compare(item.Key, splitItem.Key) < 0) ? y : newRight;
                    }
                    else
                        x = y;
                }
            }
        }
        private bool Remove( KeyValuePair<TKey,TValue> item, bool checkValue)
        {
            throw new NotImplementedException();
        }
        private BTreeNode FindNode( TKey key, out int idx)
        {
            var run = root;
            while (true)
            {
                int ip = run.FindItem(key);
                if (ip >= 0)
                {
                    idx = ip;
                    return run;
                }
                else
                {
                    if (run.IsLeaf)
                    {
                        idx = 0;
                        return null;
                    }
                    else
                    {
                        run = ((BTreeInnerNode)run).children[-ip - 1];
                    }
                }
            }
        }
        #endregion

        #region Misc
        public bool ContainsValue( TValue value )
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Debug
#if DEBUG
        public List<KeyValuePair<TKey,TValue>> TraverseRecursive(bool print)
        {
            var lst = new List<KeyValuePair<TKey, TValue>>();
            TraverseRecursive(lst, root,print,0);
            return lst;
        }
        private void TraverseRecursive(List<KeyValuePair<TKey, TValue>> lst , BTreeNode node ,bool print,int depth)
        {
            string empty = new string(' ', depth * 4);
            if( node.IsLeaf )
            {
                var nodeLeaf = (BTreeLeafNode)node;
                for (int i = 0; i < nodeLeaf.NumItems; i++)
                {
                    lst.Add(nodeLeaf.GetItem(i));
                    if(print)
                        Debug.WriteLine(empty + nodeLeaf.ID + ":" + nodeLeaf.GetItem(i));
                }
            }
            else
            {
                var nodeInner = (BTreeInnerNode)node;
                for(int i=0;i<nodeInner.NumChildren;i++)
                {
                    TraverseRecursive(lst, nodeInner.children[i],print,depth+1);
                    if (i < nodeInner.NumItems)
                    {
                        lst.Add(nodeInner.GetItem(i));
                        if(print)
                            Debug.WriteLine(empty + nodeInner.ID + ":" + nodeInner.GetItem(i));
                    }
                }
            }
        }
#endif
        #endregion
    }
}