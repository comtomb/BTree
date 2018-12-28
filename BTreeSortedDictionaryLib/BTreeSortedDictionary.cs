/*
MIT License

Copyright (c) 2019 comtomb [TomB]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace TomB.Util.Collections
{
	/// <summary>
	/// Implementation of an IDictionary with sorted Keys
	/// 
	/// Based on a B-Tree
	/// 
	/// special:
	/// 	- Single Pass Insert: Insert is done without recursion and in a single pass down (no "upward" operations)
	///  	- Single Pass Remove: Remove is done without recursion and in a single pass down (no "upward" operations)
	/// 	- get/remove the entry with the smallest key
	/// 	- get/remove the entry with the greatest key	
	/// 	- retrieve a subset of the Entries
	/// 			- all entries greater or equal X
	/// 			- all entries less or equal Y
	/// 			- all entries (greater or equal X) and (less or equal Y) =  Range X-Y  
	/// 
	/// </summary>
	[DebuggerDisplay("Count={Count} Degree={Degree} Depth={Depth}")]
	public class BTreeSortedDictionary<TKey, TValue> : IDictionary<TKey, TValue>,IReadOnlyDictionary<TKey,TValue>
    {
        #region Nodes
        /// <summary>
        /// Abstract Node of the BTree 
        /// Instantiated as either Leaf or InnerNode
        /// 
        /// 
        /// </summary>
        [DebuggerDisplayAttribute("ID = {ID} Num={numItems}")]
        private abstract class BTreeNode
        {
        	/// <summary>
        	/// true for leafs, false for inner
        	/// </summary>
        	public bool isLeaf;
        	/// <summary>
        	/// just for debugging purposes: each node in the tree has a unique id
        	/// </summary>
            public long ID;
            /// <summary>
            /// the tree this nodes belongs to 
            /// </summary>
            protected readonly BTreeSortedDictionary<TKey, TValue> tree;
            /// <summary>
            /// comparer for keys
            /// </summary>
            protected readonly IComparer<TKey> comparer;
            /// <summary>
            /// the degree of the tree: each node must have at least degree-1 items (except for root) and maximum degree*2-1 items 
            /// </summary>
            protected readonly int degree;
            /// <summary>
            /// the items
            /// </summary>
            public readonly KeyValuePair<TKey, TValue>[] items;
            /// <summary>
            /// actual used number of items in the node    degree-1 &lt;= numItems &lt;= degree*2-1  
            /// </summary>
            public int numItems;
            /// <summary>
            /// convenience: Maximum number of items (degree*2-1)
            /// </summary>
            public int MaxItems
            {
                get
                {
                    return degree * 2 - 1;
                }
            }
            /// <summary>
            /// convenience: Minimum number of items (degree-1)
            /// </summary>
            public int MinItems
            {
                get
                {
                    return degree - 1;
                }
            }

			/// <summary>
			/// constructor for an abstract BTreeNode
			/// </summary>
			/// <param name="degree">degree of the tree</param>
			/// <param name="tree">tree for this node</param>
			/// <param name="comparer">key-comparer</param>
            protected BTreeNode(int degree, BTreeSortedDictionary<TKey,TValue> tree, IComparer<TKey> comparer)
            {
                this.tree = tree;
                this.comparer = comparer;
                this.degree = degree;
                // allocate space for all items
                items = new KeyValuePair<TKey, TValue>[degree * 2 - 1];
                ID = tree.nextNodeId++;
            }
			/// <summary>
			/// Find an item in this node
			/// </summary>
			/// <param name="k">key to be found</param>
			/// <returns>&ge;=0 for the index where k was found, else -index-1 for the insertion point</returns>
            public int FindItem(TKey k)
            {
                // classic binary search in a sorted array
                int low = 0;
                int hi = numItems - 1;
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
			/// <summary>
			/// Split the (full) node
			/// example:
			/// 	this: ABCDEFG
			/// 	split -> 
			/// 		this: ABC
			/// 		newRight: EFG
			/// 		splitItem: D
			/// </summary>
			/// <param name="splitItem">the remaining (mid) item</param>
			/// <returns>created right half</returns>
            public abstract BTreeNode Split(out KeyValuePair<TKey, TValue> splitItem);
            /// <summary>
            /// merge this node with another one
			/// example:
			/// 	this: ABC
			/// 	right: EFG
			/// 	insertItem:D
			/// will result in this to be ABCDEFG 
            /// </summary>
            /// <param name="right">right half</param>
            /// <param name="insertItem">item to be inserted in the middle</param>
            public abstract void Merge(BTreeNode right, KeyValuePair<TKey, TValue> insertItem);

        }
        /// <summary>
        /// helper class for inner nodes: Store an item and a link to another node
        /// </summary>
        class InnerNodeEntry
        {
            public KeyValuePair<TKey, TValue> item;
            public BTreeNode child;

            public InnerNodeEntry(KeyValuePair<TKey, TValue> item, BTreeNode child)
            {
                this.item = item;
                this.child = child;
            }
        }
        /// <summary>
        /// Inner Node
        /// </summary>
        private class BTreeInnerNode : BTreeNode
        {
        	/// <summary>
        	/// all children of the node
        	/// there are always numItem+1 children:
        	/// 	item[i] has children[i] as left child, and children[i+1] as right child
        	/// </summary>
            public BTreeNode[] children;
            /// <summary>
            /// constructor
            /// </summary>
            /// <param name="degree"></param>
            /// <param name="tree"></param>
            /// <param name="comparer"></param>
            public BTreeInnerNode(int degree, BTreeSortedDictionary<TKey, TValue> tree, IComparer<TKey> comparer)
                : base(degree,tree, comparer)
            {
                children = new BTreeNode[degree * 2];
                isLeaf=false;
            }
            public override BTreeNode Split(out KeyValuePair<TKey, TValue> splitItem)
            {
                var newRight = new BTreeInnerNode(degree, tree, comparer);
                int mid = MaxItems / 2;
                int len = MaxItems / 2;
                splitItem = items[mid];
				// copy items&children
                Array.Copy(items, mid + 1, newRight.items, 0, len);
                Array.Copy(children, mid + 1, newRight.children, 0, len + 1);
                newRight.numItems = len;
                numItems = len;
                
                // cleanup null
                for (int i = 0; i < len; i++)
                {
                    children[mid + 1 + i] = null;
					items[mid + 1 + i] = default(KeyValuePair<TKey, TValue>);
	            }
                children[mid+len+1]=null;                
                return newRight;
            }
            public override void Merge(BTreeNode right, KeyValuePair<TKey, TValue> insertItem)
            {
                items[numItems++] = insertItem;
                Array.Copy(right.items, 0, items, numItems, right.numItems);
                Array.Copy( ((BTreeInnerNode)right).children, 0, children, numItems,right.numItems + 1);
                numItems += right.numItems;
                right.numItems = 0;
            }
            /// <summary>
            /// Insert an Item and its right child
            /// </summary>
            /// <param name="idx">insert position </param>
            /// <param name="item">new item</param>
            /// <param name="rightChild">right child for the new item</param>
            public void InsertItemWithRightChild(int idx, KeyValuePair<TKey, TValue> item, BTreeNode rightChild)
            {
                Array.Copy(items, idx, items, idx + 1, numItems - idx);
                items[idx] = item;
                Array.Copy(children, idx+1, children, idx + 2, numItems + 1 - idx-1);
                children[idx+1] = rightChild;
                numItems++;
            }
            /// <summary>
            /// Insert an Item and its left child
            /// </summary>
            /// <param name="idx">insert position </param>
            /// <param name="item">new item</param>
            /// <param name="leftChild">left child for the new item</param>
            public void InsertItemWithLeftChild(int idx, KeyValuePair<TKey, TValue> item, BTreeNode leftChild)
            {
                Array.Copy(items, idx, items, idx + 1, numItems - idx);
                items[idx] = item;
                Array.Copy(children, idx, children, idx + 1, numItems + 1 - idx);
                children[idx] = leftChild;
                numItems++;
            }
            /// <summary>
            /// Remove an Item including its left child
            /// </summary>
            /// <param name="idx"></param>
            /// <returns></returns>
            public InnerNodeEntry RemoveItemWithLeftChild(int idx)
            {
                var retItem = items[idx];
                Array.Copy(items, idx + 1, items, idx, numItems - idx - 1);
                items[numItems - 1] = default(KeyValuePair<TKey, TValue>);
                var retChild = children[idx ];
                Array.Copy(children, idx + 1, children, idx, numItems - idx );
                children[numItems] = null;
                numItems--;
                return new InnerNodeEntry(retItem, retChild);
            }
            /// <summary>
            /// remove an item including it's right child
            /// </summary>
            /// <param name="idx"></param>
            /// <returns></returns>
            public InnerNodeEntry RemoveItemWithRightChild(int idx)
            {
                var retItem = items[idx];
                Array.Copy(items, idx + 1, items, idx, numItems - idx - 1);
                items[numItems - 1] = default(KeyValuePair<TKey, TValue>);
                var retChild = children[idx + 1];
                Array.Copy(children, idx + 2, children, idx + 1, numItems+1-(idx+1)-1);
                children[numItems] = null;
                numItems--;
                return new InnerNodeEntry(retItem, retChild);
            }
        }
        /// <summary>
        /// Leaf Node
        /// </summary>
        private class BTreeLeafNode : BTreeNode
        {
        	/// <summary>
        	/// constructor
        	/// </summary>
        	/// <param name="degree"></param>
        	/// <param name="tree"></param>
        	/// <param name="comparer"></param>
            public BTreeLeafNode(int degree,BTreeSortedDictionary<TKey, TValue> tree, IComparer<TKey> comparer)
                :base(degree,tree,comparer)
            {
                isLeaf=true;
            }
            /// <summary>
            /// Remove an item
            /// </summary>
            /// <param name="idx"></param>
            /// <returns></returns>
            public KeyValuePair<TKey,TValue> RemoveItem(int idx)
            {
                var ret = items[idx];
                Array.Copy(items, idx + 1, items, idx, numItems - idx - 1);
                items[numItems - 1] = default(KeyValuePair<TKey, TValue>);
                numItems--;
                return ret;
            }
            /// <summary>
            /// Insert an Item
            /// </summary>
            /// <param name="idx"></param>
            /// <param name="item"></param>
            public void InsertItem(int idx, KeyValuePair<TKey, TValue> item)
            {
                Array.Copy(items, idx, items, idx + 1, numItems - idx);
                items[idx] = item;
                numItems++;
            }
            /// <summary>
            /// Split see base
            /// </summary>
            /// <param name="splitItem"></param>
            /// <returns></returns>
            public override BTreeNode Split(out KeyValuePair<TKey, TValue> splitItem)
            {
                var newRight = new BTreeLeafNode(degree, tree, comparer);
                int mid = MaxItems / 2;
                int len = MaxItems / 2;
                splitItem = items[mid];

                Array.Copy(items, mid + 1, newRight.items, 0, len);
                newRight.numItems = len;
                numItems = len;
                // cleanup
                for (int i = 0; i < len; i++)
                    items[mid + 1 + i] = default(KeyValuePair<TKey, TValue>);
                return newRight;
            }
            /// <summary>
            /// merge see base
            /// </summary>
            /// <param name="right"></param>
            /// <param name="insertItem"></param>
            public override void Merge(BTreeNode right, KeyValuePair<TKey, TValue> insertItem)
            {
                items[numItems++] = insertItem;
                Array.Copy(right.items, 0, items, numItems, right.numItems);
                numItems += right.numItems;
                right.numItems = 0;
            }
        }
        #endregion
        #region Enumerators & Collections
        /// <summary>
        /// (read only) Collection of all Values of the tree
        /// </summary>
        [DebuggerDisplay("Count={Count}")]
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
            /// <summary>
            /// create a new read only collection
            /// </summary>
            /// <param name="tree"></param>
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
            	if( array==null)
            		throw new ArgumentNullException();
            	if(arrayIndex<0 || arrayIndex>array.Length)
            		throw new ArgumentException();
            	if( array.Length-arrayIndex<Count)
            		throw new ArgumentException();
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
        /// <summary>
        /// (read only) Collection of all keys 
        /// </summary>
        [DebuggerDisplay("Count={Count}")]
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
            	if( array==null)
            		throw new ArgumentNullException();
            	if(arrayIndex<0 || arrayIndex>array.Length)
            		throw new ArgumentException();
            	if( array.Length-arrayIndex<Count)
            		throw new ArgumentException();
            	
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
        /// <summary>
        /// Enumerator of the tree
        /// 
        /// the enumerator uses a Stack to walk through the tree 
        /// </summary>
        private class KeyValueEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
        	/// <summary>
        	/// stack entry
        	/// </summary>
            private class NodeInfo
            {
                public readonly BTreeNode node;
                public readonly int index;
                public NodeInfo(BTreeNode n, int i)
                {
                    node = n;
                    index = i;
                }
            }
            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                	// check if the tree is unmodified
                    if (tree.modCount != modCount)
                        throw new NotSupportedException();
                    if (runNode != null)
                        return runNode.items[runIdx];
                    else
                        throw new NotSupportedException();
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (tree.modCount != modCount)
                        throw new NotSupportedException();
                    if (runNode != null)
                        return runNode.items[runIdx];
                    else
                        throw new NotSupportedException();
                }
            }
            /// <summary>
            /// the tree we walk through...
            /// </summary>
            private readonly BTreeSortedDictionary<TKey, TValue> tree;
            /// <summary>
            /// initial modification count of the tree. stop enumerating whenever the tree is changed 
            /// </summary>
            private int modCount;
            /// <summary>
            /// stack for iteration
            /// </summary>
            private readonly Stack<NodeInfo> stack = new Stack<NodeInfo>();
            /// <summary>
            /// current node
            /// </summary>
            private BTreeNode runNode;
            /// <summary>
            /// current index
            /// </summary>
            private int runIdx;
            /// <summary>
            /// maximum key where the enumerator should stop (or default(TKey) if we enumerate to the end
            /// </summary>
            private readonly TKey maxKey;
            /// <summary>
            /// simple constructor
            /// </summary>
            /// <param name="tree"></param>
            public KeyValueEnumerator(BTreeSortedDictionary<TKey,TValue> tree)
            	: this(tree,default(TKey),default(TKey))
            {
            	
            }
            /// <summary>
            /// constructor with range
            /// </summary>
            /// <param name="tree"></param>
            /// <param name="minKey">starting key, or default(TKey) for smallest tree entry</param>
            /// <param name="maxKey">end key or defaulr(TKey) for greatest key entry</param>
            public KeyValueEnumerator(BTreeSortedDictionary<TKey, TValue> tree, TKey minKey, TKey maxKey)
            {
                this.tree = tree;
                modCount = tree.modCount;
                this.maxKey=maxKey;
                if( !minKey.Equals(default(TKey) ))
                {
                	//  
                	// if we have a minimum key pre-populate the stack
                	// ugly solution:
                	// we walk down to the node where minKey resides
                	// if we found minkey in a innernode:
					//     	we walk down to the predecessor of minKey
                	// later the first call to MoveNext() unrolls the stack an places runNode and runIdx at the correct postion: minKey 
                	var run=tree.root;
                	while(true)
                	{
                		int idx=run.FindItem(minKey);
                		if(idx>=0)
                		{
                			if( run.isLeaf)
                			{
                				// that's easy...
	            				runNode=run;
	            				runIdx=idx-1; // -1 as MoveNext triggers ths runIdx++;
                				break;
                			}
                			else
                			{
                				stack.Push(new NodeInfo(run,idx));  // here we want to land...
                				// walk down
                				run=((BTreeInnerNode)run).children[idx];
                				while( !run.isLeaf )
                				{
                					stack.Push(new NodeInfo(run,run.numItems));               				
                					idx=run.numItems+1;
                					run=((BTreeInnerNode)run).children[run.numItems];
                				}
	            				runNode=run;
	            				runIdx=run.numItems;	// place at last item of leaf 
                				break;
                			}
                		}
                		else
                		{
                			if(run.isLeaf)
                			{
                				runNode=run;
                				runIdx=-idx-1  -1;
                				break;
                			}
                			else
                			{
                				stack.Push(new NodeInfo(run,-idx-1));
                				run=((BTreeInnerNode)run).children[-idx-1];
                			}
                		}
                	}
                }
            }

            public void Dispose()
            {
                
            }

            public bool MoveNext()
            {
            	
                if (tree.modCount != modCount)
                    throw new NotSupportedException();
                // start with root ==null & runIdx -1
                if (runNode == null)
                {
                    if (runIdx == -2)
                        return false; // already at end
                    runNode = tree.root;
                    runIdx = -1;
                }
                runIdx++;	// the actual "next"
                while (true)
                {
                    if (runNode.isLeaf)
                    {
                        if (runIdx < runNode.numItems)
                        {
                        	if( !maxKey.Equals(default(TKey))) // abort if >max
                        		return tree.comparer.Compare(runNode.items[runIdx].Key,maxKey)<=0;                        	
                            return true;
                        }
                        // else we step up the stack
                    }
                    else
                    {
                        // try to walk down 
                        if (runIdx <= runNode.numItems)
                        {
                            stack.Push(new NodeInfo(runNode, runIdx));
                            runNode = ((BTreeInnerNode)runNode).children[runIdx];
                            runIdx = 0;
                            continue;
                        }
                    }
                    // check if all done
                    if (stack.Count == 0)
                    {
                        runNode = null;
                        runIdx = -2; // trigger a fail if call MoveNext() again
                        return false;
                    }
                    // up again
                    var p = stack.Pop();
                    runNode = p.node;
                    runIdx = p.index;
                    if (runIdx < runNode.numItems)
                    {                    	
                    	if( !maxKey.Equals(default(TKey)))
                    		return tree.comparer.Compare(runNode.items[runIdx].Key,maxKey)<=0;
                        return true;	// here we return for the item in an inner node
                    }
                    // next 
                    runIdx++;
                }
            }

            public void Reset()
            {
                runNode = null;
                runIdx = -1;
                modCount = tree.modCount;
            }
        }
        /// <summary>
        /// enumerator for the ValueCollection. Just a wrapper around the KeyValueEnumerator
        /// </summary>
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
        /// <summary>
        /// enumerator for the KeyCollection. Just a wrapper around the KeyValueEnumerator
        /// </summary>
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
        /// <summary>
        /// degree of the tree
        /// </summary>
        private readonly int degree;
        /// <summary>
        // Key comparer
        /// </summary>
        private readonly IComparer<TKey> comparer;
        /// <summary>
        /// root node of the tree
        /// </summary>
        private BTreeNode root;
        /// <summary>
        /// current number of items in the tree
        /// </summary>
        private int count;
        /// <summary>
        /// modification counter: each change in the tree changes this value. 
        /// this causes an enumerator to fail if there is a concurrent modificatio
        /// </summary>
        private int modCount;
        /// <summary>
        /// mask to avoid overflows when continously incrementing the modCount. 
        /// </summary>
        private const int ModCountMask = 0xfffffff;	
        /// <summary>
        /// maximum number of items in a node (=degree*2-1)
        /// </summary>
        private readonly int maxItems;
        // minimum number of items in a node (=degree-1)
        private readonly int minItems;
        /// <summary>
        /// Default degree for a tree
        /// </summary>
        public const int DefaultDegree=257;
        /// <summary>
        /// ID for the next node (for debugging purposes only)
        /// </summary>
        private long nextNodeId = 1;
        /// <summary>
        /// Degree of the tree
        /// </summary>
        public int Degree
        {
        	get
        	{
        		return degree;
        	}
        }
        /// <summary>
        /// Depth of the tree
        /// </summary>
        public int Depth
        {
        	get;private set;
        }
        #endregion
        #region Constructors
        /// <summary>
        /// standard constructor
        /// </summary>
        public BTreeSortedDictionary()
        	: this(DefaultDegree,null)
        {
        	
        }
        /// <summary>
        /// work horse constructor: degree & comparer
        /// </summary>
        /// <param name="degree">Degree for the tree. either -1 for default, or &ge;3</param>
        /// <param name="comparer">comparer for keys, or null to use default</param>
        public BTreeSortedDictionary(int degree=-1,IComparer<TKey> comparer=null)
        {
        	if( degree==-1)
        		degree=DefaultDegree;
        	else
        		if( degree<3)
        			throw new ArgumentException("invalid degree");
            this.degree = degree;
            maxItems = degree * 2 - 1;
            minItems = degree - 1;
            this.comparer = comparer ?? Comparer<TKey>.Default;
            root = new BTreeLeafNode(degree, this, this.comparer); 
            Depth=1;
        	
        }
        /// <summary>
        /// copy-constructor
        /// </summary>
        /// <param name="src"></param>
        /// <param name="degree"></param>
        /// <param name="comparer"></param>
        public BTreeSortedDictionary(IDictionary<TKey,TValue> src,int degree=-1,IComparer<TKey> comparer=null)
        	:this(degree,comparer)
        {
            if(src!=null )
            	AddAll(src);        	
        }
        
        #endregion
        #region IDictionary
        /// <summary>
        /// <see cref="IDictionary"/>
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
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
                node.items[idx]=new KeyValuePair<TKey,TValue>(key, value);
            }
        }
        /// <summary>
        /// <see cref="IDictionary.Keys"/>
        /// </summary>
        public ICollection<TKey> Keys
        {
            get
            {
                return new KeyCollection(this);
            }
        }
        /// <summary>
        /// <see cref="IDictionary.Values"/>
        /// </summary>
        public ICollection<TValue> Values
        {
            get
            {
                return new ValueCollection(this);
            }
        }
        /// <summary>
        /// <see cref="IReadOnlyDictionary"/
        /// </summary>
        IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys
        {
        	get
        	{
        		return new KeyCollection(this);
        	}
        }
        /// <summary>
        /// <see cref="IReadOnlyDictionary"/
        /// </summary>
        IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values
        {
        	get
        	{
        		return new ValueCollection(this);
        	}
        }
        /// <summary>
        /// <see cref="ICollection{TKey, TValue}.Count"/>
        /// </summary>
        public int Count
        {
            get
            {
                return count;
            }
        }
        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}.IsReadOnly"/>
        /// </summary>
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(TKey key, TValue value)
        {
            AddNew(new KeyValuePair<TKey,TValue>(key, value));
        }
        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/>
        /// </summary>
        /// <param name="item"></param>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            if (!AddNew(item))
                throw new ArgumentException();
        }
        /// <summary>
        /// <see cref="ICollection{T}.Clear"/>
        /// </summary>
        public void Clear()
        {
            root = new BTreeLeafNode(degree, this, comparer);
            count = 0;
            modCount = (modCount + 1) & ModCountMask;
            Depth=1;
        }
        /// <summary>
        /// <see cref="ICollection{T}.Contains(T)"/>
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue v;
                        
            if (!TryGetValue(item.Key, out v))
                return false;
            return v.Equals(item.Value);
        }
        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}.ContainsKey(TKey)"/>
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(TKey key)
        {
        	TValue v;
            return TryGetValue(key, out  v);
        }
        /// <summary>
        /// <see cref="ICollection{T}.CopyTo(T[], int)"/>
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
        	if( array==null)
        		throw new ArgumentNullException();
        	if(arrayIndex<0 || arrayIndex>array.Length)
        		throw new ArgumentException();
        	if( array.Length-arrayIndex<Count)
        		throw new ArgumentException();
            foreach (var kv in this)
                array[arrayIndex++] = kv;
        }
        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}"/>
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
        	return new KeyValueEnumerator(this);
        }
        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}.Remove(TKey)"/>
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            return Remove(new KeyValuePair<TKey,TValue>(key,default(TValue)),false);
        }
        /// <summary>
        /// <see cref="ICollection{T}.Remove(T)"/>
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item, true);
        }
        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            int idx;
            var node = FindNode(key, out idx);
            if (node == null)
            {
                value = default(TValue);
                return false;
            }
            value = node.items[idx].Value;
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new KeyValueEnumerator(this);
        }
        #endregion
        #region Core
        /// <summary>
        /// Add a new Item
        /// 
        /// strategy: single pass, no recursion
        /// when we walk down the tree to insert into a node we make sure that all nodes on the way have less then maxKeys items. 
        /// Doing so we ensure that in the final step there is room for an insert in the leaf
        ///   
        /// </summary>
        /// <param name="item">new item to be added</param>
        /// <returns><code>true</code> if added, <code>false</code> if key already existing</returns>
        private bool AddNew(KeyValuePair<TKey,TValue> item )
        {
        	// check if we have to grow the tree
            if( root.numItems==maxItems)
            {
            	// split the root and create a new InnerNode as new root.
                KeyValuePair<TKey, TValue> splitItem;
                var newRight = root.Split(out splitItem);
                var newRoot = new BTreeInnerNode(degree, this, comparer);
                // make the splitItem to be the new single entry in newroot, and link it to oldRoot and newRight 
                newRoot.InsertItemWithRightChild(0, splitItem, newRight);
                newRoot.children[0] = root;
                root = newRoot;
                Depth++;
            }
            // walk down to a leaf
            var x = root;
            while(true )
            {
                int ip = x.FindItem(item.Key);
                if (ip >= 0)
                    return false;	// key already there
                ip = -ip - 1;
                if(x.isLeaf)
                {
                	// there is enough room in the leaf --> insert 
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
                    if (y.numItems == maxItems)
                    {	// split the child (Leaf or inner!) 
                        KeyValuePair<TKey, TValue> splitItem;
                        var newRight = y.Split(out splitItem);
                        // and link it 
                        xInner.InsertItemWithRightChild(ip, splitItem, newRight);
                        x = (comparer.Compare(item.Key, splitItem.Key) < 0) ? y : newRight;
                    }
                    else
                        x = y;
                }
            }
        }
        /// <summary>
        /// Remove an item from the tree
        /// 
        /// Strategy: single pass down
        /// each node on the way down to the key will have more than minKeys Entries. We rotate or merge to ensure this.
		/// Doing so we can savely delete the found key
		/// if the key is in a leaf we can simply delete it
		/// if the key is in in an inner node we replace it with the successor or predecessor (which is always in a leaf) and delete then
		/// if necessary the tree shrinks
        /// </summary>
        /// <param name="item">item to be removed</param>
        /// <param name="checkValue">if this is <code>true</code> also the item.value needs to match</param>
        /// <returns></returns>
        private bool Remove( KeyValuePair<TKey,TValue> item, bool checkValue)
        {
            var x = root;
            // walk down
            while(true)
            {
                int idx = x.FindItem(item.Key);
                if(x.isLeaf)
                {
                    if(idx<0)
                    {
                        return false;	// we are in a leaf an the key is not here
                    }
                    else
                    {
                    	// that's easy... simply delete
                        ((BTreeLeafNode)x).RemoveItem(idx);
                        count--;
                        modCount = (modCount + 1) & ModCountMask;
                        return true;
                    }
                }
                else
                {
                    if(idx>=0)
                    {	 
                        var xInner = (BTreeInnerNode)x;
                        // found in an inner node. we have three cases:
                        //  1: we can use predecessor (right child)          
                        //  2: we can use successor (left child)
                        //  3: we can/must merge left and right child
                        var left = xInner.children[idx];
                        if(left.numItems>minItems)
                        {
                            // case 1: walk down to the predeccessor: most right node in the mostright leaf of the left child
                            var x2 = x;
                            int idx2 = idx;
                            do
                            {
                                x2 = EnsureDeleteInChild((BTreeInnerNode)x2, idx2);
                                if(!x2.isLeaf)
                                	idx2 = x2.numItems; // numChildren-1;
                            } while (!x2.isLeaf);
                            // remove the predecessor from the leaf
                            var max = ((BTreeLeafNode)x2).RemoveItem(x2.numItems - 1);
                            x.items[idx]= max; // and put the predecessor into the inner node and overwrite key
                            modCount = (modCount + 1) & ModCountMask;
                            count--;
                            return true;
                        }
                        else
                        {
                            var right = xInner.children[idx + 1];
                            if(right.numItems>minItems)
                            {
                                // case 2: walk down to the successor: most left node of the mostleft leaf of the right child
                                BTreeNode x2 = x;
                                int idx2 = idx + 1;
                                do
                                {
                                    x2 = EnsureDeleteInChild((BTreeInnerNode)x2, idx2);
                                    idx2 = 0;
                                } while (!x2.isLeaf);
                                // remove smallest entry from leaf, and bring it to the inner node (overwrite the entry to be deleted) 
                                var min = ((BTreeLeafNode)x2).RemoveItem(0);
                                x.items[idx]= min;
                                modCount = (modCount + 1) & ModCountMask;
                                count--;
                                return true;
                            }
                            else
                            {
                                // case 3: merge left&right child use key as the midItem
                                left.Merge(right, x.items[idx]);
                                ((BTreeInnerNode)x).RemoveItemWithRightChild(idx);	// delete the key and the 'empt' right child
                                if (x == root && x.numItems == 0)
                                {	// shrink tree
                                    root = ((BTreeInnerNode)x).children[0]; 
                                    Depth--;
                                }
                                x = left;
                                idx = minItems;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // next child
                        idx = -idx - 1;
                        x = EnsureDeleteInChild( (BTreeInnerNode)x, idx);	// make sure that the child at idx has enough items
                    }
                }
            }
        }
        /// <summary>
        /// Ensure the child has enough items
        /// </summary>
        /// <param name="x"></param>
        /// <param name="idx"></param>
        /// <returns>the child</returns>
        private BTreeNode EnsureDeleteInChild( BTreeInnerNode x,int idx)
        {
            BTreeNode y = x.children[idx];	// the child we look at
            if (y.numItems > minItems)
                return y; // nothing to todo

            // left & right siblings of y
            var left = idx > 0 ? x.children[idx - 1] : null;
            var right = idx < x.numItems ? x.children[idx + 1] : null;
            // approach:
            //  1. try to borrow from left sibling
            //  2. try to borrow from right sibling
            //  3. try to merge with left sibling
            //  4. merge with right sibling
            if(left!=null && left.numItems>minItems)
            {
                // borrow from left sibling: rotation
                KeyValuePair<TKey, TValue> mid;
                if(left.isLeaf)
                {
                    mid = ((BTreeLeafNode)left).RemoveItem(left.numItems - 1);
                    ((BTreeLeafNode)y).InsertItem(0, x.items[idx - 1]);
                }
                else
                {
                    var e = ((BTreeInnerNode)left).RemoveItemWithRightChild(left.numItems - 1);
                    ((BTreeInnerNode)y).InsertItemWithLeftChild(0, x.items[idx - 1], e.child);
                    mid = e.item;
                }
                x.items[idx - 1]= mid;
                return y;
            }
            else
            {
                if(right!=null && right.numItems>minItems)
                {
                    // borrow from right sibling: rotation
                    KeyValuePair<TKey, TValue> mid;
                    if (right.isLeaf)
                    {
                        mid = ((BTreeLeafNode)right).RemoveItem(0);
                        ((BTreeLeafNode)y).InsertItem(y.numItems, x.items[idx]);
                    }
                    else
                    {
                        var e = ((BTreeInnerNode)right).RemoveItemWithLeftChild(0);
                        ((BTreeInnerNode)y).InsertItemWithRightChild(y.numItems, x.items[idx],e.child);
                        mid = e.item;
                    }
                    x.items[idx]=mid;
                    return y;
                }
                else
                {
                    if(left!=null && left.numItems==minItems)
                    {
                        // merge with left
                        left.Merge(y, x.items[idx-1]);
                        x.RemoveItemWithRightChild(idx - 1);
                        if (x == root && !x.isLeaf && x.numItems == 0)
                        {
                            root = x.children[0]; // shrink tree
                            Depth--;
                        }
                        return left;
                    }
                    else
                    {
                        // merge with right
                        y.Merge(right, x.items[idx]);
                        x.RemoveItemWithRightChild(idx);
                        if (x == root && !x.isLeaf && x.numItems == 0)
                        {
                            root = x.children[0];
                            Depth--;
                        }
                        return y;
                    }
                }
            }
        }
        /// <summary>
        /// Find the node where key is stored
        /// </summary>
        /// <param name="key">key to be found</param>
        /// <param name="idx">index of the key in the found node</param>
        /// <returns>Node where key is found, or <code>null</code> if key is not found</returns>
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
                    if (run.isLeaf)
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
        /// <summary>
        /// check if a value is contained in the tree
        /// </summary>
        /// <param name="value"></param>
        /// <returns><code>true</code> if the value is in the tree</returns>
        public bool ContainsValue( TValue value )
        {
        	foreach(var kv in this) 
        		if( kv.Value.Equals(value))
        		   return true;
        	return false;
        }
        /// <summary>
        /// number of Nodes
        /// </summary>
        /// <returns>number of Nodes</returns>
        public int CountNodes()
        {
        	return CountNodes(root);
        }
        /// <summary>
        /// worker to recursive count the nodes in the tree
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private int CountNodes( BTreeNode node )
        {
        	if( node.numItems<degree-1 && node!=root)
        		throw new Exception();
        	if( node.isLeaf )
        		return 1;
        	int s=1;
        	var inner=(BTreeInnerNode)node;
        	for(int i=0;i<=inner.numItems;i++)
        		s+=CountNodes(inner.children[i]);
        	return s;
        }
        /// <summary>
        /// find the smallest item in the tree
        /// </summary>
        /// <returns>smallest item or <code>null</code> if tree is empty</returns>
        public KeyValuePair<TKey,TValue> GetSmallest()
        {
        	if( count==0)
        		return default(KeyValuePair<TKey,TValue>);
        	var run=root;
        	while(!run.isLeaf)
        	{
        		run=((BTreeInnerNode)run).children[0];
        	}
        	return run.items[0];
        }
        /// <summary>
        /// remove the smallest item
        /// </summary>
        /// <exception cref="InvalidOperationException"> if tree is empty</exception
        /// <returns>smallest item</returns>
        public KeyValuePair<TKey,TValue> RemoveSmallest()
        {
        	if( count==0)
        		throw new InvalidOperationException();
        	var run=root;
        	while(!run.isLeaf)
        	{
        		run=EnsureDeleteInChild( (BTreeInnerNode)run,0);
        	}
        	count--;
        	modCount = (modCount + 1) & ModCountMask;
        	return ((BTreeLeafNode)run).RemoveItem(0);
        }
        /// <summary>
        /// find the greatest item in the tree
        /// </summary>
        /// <returns>greatest item or <code>default(KeyValuePair)</code> if tree is empty</returns>
        public KeyValuePair<TKey,TValue> GetGreatest()
        {
        	if( count==0)
        		return default(KeyValuePair<TKey,TValue>);
        	var run=root;
        	while(!run.isLeaf)
        	{
        		run=((BTreeInnerNode)run).children[run.numItems];
        	}
        	return run.items[run.numItems-1];
        }
        /// <summary>
        /// remove greatest item
        /// </summary>
        /// <exception cref="InvalidOperationException"> if tree is empty</exception
        /// <returns>greatest item</returns>
        public KeyValuePair<TKey,TValue> RemoveGreatest()
        {
        	if( count==0)
        		throw new InvalidOperationException();
        	var run=root;
        	while(!run.isLeaf)
        	{
        		run=EnsureDeleteInChild( (BTreeInnerNode)run,run.numItems);
        	}
        	count--;
        	modCount = (modCount + 1) & ModCountMask;
        	return ((BTreeLeafNode)run).RemoveItem(run.numItems-1);
        }
        /// <summary>
        /// get an enumerator for a range of keys
        /// </summary>
        /// <param name="minKey">minimum key</param>
        /// <param name="maxKey">maximum key</param>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumeratorRange( TKey minKey, TKey maxKey )
        {
        	return new KeyValueEnumerator(this,minKey,maxKey);
        }
        /// <summary>
        /// get an enumerator for all elements &gt;=minKey
        /// </summary>
        /// <param name="minKey">minimum key</param>
        /// <returns>enumerator</returns>
        public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumeratorGreaterOrEqual( TKey minKey )
        {
        	return new KeyValueEnumerator(this,minKey,default(TKey));
        }
        /// <summary>
        /// get enumerator for all elements &lt;=maxKey
        /// </summary>
        /// <param name="maxKey">maximum</param>
        /// <returns>enumerator</returns>
        public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumeratorLessOrEqual( TKey maxKey )
        {
        	return new KeyValueEnumerator(this,default(TKey),maxKey);
        }
        /// <summary>
        /// get a part of the dictionary as a collection
        /// </summary>
        /// <param name="minKey">minimum</param>
        /// <param name="maxKey">maximum</param>
        /// <returns></returns>
        public ICollection<KeyValuePair<TKey,TValue>> GetRange(TKey minKey,TKey maxKey)
        {
        	var lst=new LinkedList<KeyValuePair<TKey,TValue>>();
        	var setEnum=new KeyValueEnumerator(this,minKey,maxKey);
        	while(setEnum.MoveNext() )
        		lst.AddLast(setEnum.Current);
        	return lst;
        }
        
        /// <summary>
        /// get the items in range
        /// </summary>
        /// <param name="minKey">minimum</param>
        /// <param name="maxKey">maximum</param>
        /// <returns></returns>
        public ICollection<TKey> GetKeyRange(TKey minKey,TKey maxKey)
        {
        	var lst=new LinkedList<TKey>();
        	var setEnum=new KeyValueEnumerator(this,minKey,maxKey);
        	while(setEnum.MoveNext() )
        		lst.AddLast(setEnum.Current.Key);
        	return lst;
        }
        /// <summary>
        /// Add all items of a collection
        /// </summary>
        /// <param name="src"></param>
        public void AddAll( ICollection<KeyValuePair<TKey,TValue>> src)
        {
        	foreach(var kv in src )
        		Add(kv);        	
        }        
        #endregion
    }
}
