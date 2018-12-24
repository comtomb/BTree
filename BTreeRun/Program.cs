using System;
using System.Collections.Generic;
using TomB.Util.Collections;

namespace BTreeRun
{
    class Program
    {

        private static void Compare<K,V>(BTreeSortedDictionary<K,V> tree,SortedDictionary<K,V> master)
        {
            if (tree.Count != master.Count)
                throw new Exception();
            var arrMaster = new KeyValuePair<K,V>[master.Count];
            master.CopyTo(arrMaster, 0);
            var arrTree = new KeyValuePair<K, V>[master.Count];
            var lstTree = tree.TraverseRecursive(false);
            if( lstTree.Count!=master.Count)
                throw new Exception();
            lstTree.CopyTo(arrTree, 0);
            for (int i = 0; i < master.Count; i++)
                if (!arrTree[i].Key.Equals(arrMaster[i].Key) || !arrTree[i].Value.Equals(arrMaster[i].Value))
                    throw new Exception();
            for(int i=0;i<master.Count;i++)
                if( !tree.ContainsKey(arrMaster[i].Key))
                    throw new Exception();

        }
        private static void Step(BTreeSortedDictionary<int,int> tree,SortedDictionary<int,int> master,int target, Random rnd )
        {
            if( target>tree.Count)
            {
                Console.WriteLine(tree.Count + "->" + target + ": add " + (target - tree.Count));
                while(tree.Count<target)
                {
                    int k;
                    do
                    {
                        k = rnd.Next(0, 999999999);
                    } while (master.ContainsKey(k));
                    tree.Add(k, -k);
                    master.Add(k, -k);
                }
            }
            else
            {
                Console.WriteLine(tree.Count + "->" + target + ": remove " + (tree.Count-target));
                throw new NotImplementedException();
            }
        }


        static void Main(string[] args)
        {
            var tree = new BTreeSortedDictionary<int, int>(30);
            var master = new SortedDictionary<int, int>();
            var rnd = new Random(0);
            Step(tree, master, 5000000, rnd);
            Compare<int, int>(tree, master);
            //tree.TraverseRecursive(true);

            Console.WriteLine();
        }
    }
}
                                           