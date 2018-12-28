# BTree
C#: SortedDictionary based on a BTree


C# Implementation of a SortedDictionary using a BTRee

- IDictionary<TKey,TValue>
- All Items are sorted by Key


BTree Implementation:
- Add/Remove are done in "single pass down", without recursion and without "up-steps"
- Implementation of IDictionary<TKey,TValue>

SubProjects:
- BTreeRun: For testing&development only
- BTreeSortedDictionaryLib: The BTree Implementation
- BTreeTest: Unit Tests
