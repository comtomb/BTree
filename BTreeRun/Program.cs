using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            tree.CopyTo(arrTree, 0);
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
                long t0 = DateTime.Now.Ticks;
                Console.Write(tree.Count + "->" + target + ": add " + (target - tree.Count)+"   ");
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
                long t1 = DateTime.Now.Ticks;
                Console.WriteLine(TimeSpan.FromTicks(t1 - t0).TotalMilliseconds);
            }
            else
            {
                long t0 = DateTime.Now.Ticks;
                Console.Write(tree.Count + "->" + target + ": remove " + (tree.Count-target)+"   ");
                var lstMaster = new List<KeyValuePair<int, int>>(master);
                int c = 0;
                while(target<tree.Count)
                {
                    int idx = rnd.Next(lstMaster.Count);
                    var k = lstMaster[idx];
                    lstMaster.RemoveAt(idx);
                    master.Remove(k.Key);
                    tree.Remove(k.Key);
                    c++;
                }
                long t1 = DateTime.Now.Ticks;
                Console.WriteLine(TimeSpan.FromTicks(t1 - t0).TotalMilliseconds);
            }
        }
        
        
        static void PerfAddSeq()
        {
        	Console.WriteLine("Add Sequential");
        	int[]degrees=new int[] {3,7,13,128};
        	var rnd=new Random(0);
        	var sd=new SortedDictionary<int,int>();
        	int toAdd=1000000;
        	var tsd0=DateTime.Now.Ticks;
        	for(int i=0;i<toAdd;i++)
        		sd.Add(i,-1);
        	var tsd1=DateTime.Now.Ticks;
        	Console.WriteLine("Add SortedDictionary "  +TimeSpan.FromTicks(tsd1-tsd0).TotalMilliseconds);
        	
        	
        	foreach( int d in degrees)
        	{
        		var tree3=new BTreeSortedDictionary<int,int>(null,d,null);
	        	var ttree0=DateTime.Now.Ticks;
	        	for(int i=0;i<toAdd;i++)
	        		tree3.Add(i,-1);
	        	var ttree1=DateTime.Now.Ticks;
	        	Console.WriteLine("Add         Tree d:" + String.Format("{0,3:D}",d) + " "   +TimeSpan.FromTicks(ttree1-ttree0).TotalMilliseconds);
        	}
        	
        	
        }

        static void PerfAddContainRemove(int toAdd)
        {
        	Console.WriteLine("Add Random " + toAdd);
        	int k1=toAdd/3;
        	int[]degrees=new int[] {3,7,13,63,128,257};
        	var rnd=new Random(0);
        	var sd=new SortedDictionary<int,int>();
        	int[] src=new int[toAdd];
        	for(int i=0;i<src.Length;i++)
        		src[i]=i;
        	for(int i=0;i<src.Length;i++)
        	{
        		int idx=rnd.Next(src.Length);
        		int h=src[i];
        		src[i]=src[idx];
        		src[idx]=h;
        	}

        	
        	var tsd0=DateTime.Now.Ticks;
        	for(int i=0;i<toAdd;i++)
        	{
        		sd.Add(src[i],-src[i]);
        	}
        	var tsd1=DateTime.Now.Ticks;
        	Console.WriteLine("Add SortedDictionary     "  +TimeSpan.FromTicks(tsd1-tsd0).TotalMilliseconds);
        	for(int i=0;i<k1;i++)
        		sd.Remove(src[i]);
        	long sum=0;
        	for(int i=k1;i<toAdd;i++)
        	{        		
        		sum+=sd[src[i]];;
        	}
        	var tsd2=DateTime.Now.Ticks;
        	Console.WriteLine("Retr. SortedDictionary   "  +TimeSpan.FromTicks(tsd2-tsd1).TotalMilliseconds);
        	for(int i=k1;i<toAdd;i++)
        		sd.Remove(src[i]);
	        var tsd3=DateTime.Now.Ticks;
        	Console.WriteLine("Remove SortedDictionary  "  +TimeSpan.FromTicks(tsd3-tsd2).TotalMilliseconds);
        	Console.WriteLine("Total SortedDictionary           "  +TimeSpan.FromTicks(tsd3-tsd0).TotalMilliseconds);
        	
        	foreach( int d in degrees)
        	{
	        	var tree3=new BTreeSortedDictionary<int,int>(null,d,null);
	        	var ttree0=DateTime.Now.Ticks;
	        	for(int i=0;i<toAdd;i++)
	        		tree3.Add(src[i],-src[i]);
	        		        	
	        	var ttree1=DateTime.Now.Ticks;
	        	Console.WriteLine("Add Tree with    d=" + String.Format("{0,4:D}",d) + ": "   +TimeSpan.FromTicks(ttree1-ttree0).TotalMilliseconds);
	        	Console.WriteLine("After Add:  Nodes: " + tree3.CountNodes() + " Depth: " + tree3.Depth() );
	        	for(int i=0;i<k1;i++)
	        		tree3.Remove(src[i]);
	        	Console.WriteLine("After Pre-Remove:  Nodes: " + tree3.CountNodes() + " Depth: " + tree3.Depth() );
	        	sum=0;
	        	for(int i=k1;i<toAdd;i++)
	        	{        		
	        		sum+=tree3[src[i]];;
	        	}
	        	var ttree2=DateTime.Now.Ticks;
	        	Console.WriteLine("Retr. Tree with  d=" + String.Format("{0,4:D}",d) + ": "   +TimeSpan.FromTicks(ttree2-ttree1).TotalMilliseconds);
	        	for(int i=k1;i<toAdd;i++)
	        		tree3.Remove(src[i]);
	        	var ttree3=DateTime.Now.Ticks;
	        	Console.WriteLine("Remove Tree with d=" + String.Format("{0,4:D}",d) + ": "   +TimeSpan.FromTicks(ttree3-ttree2).TotalMilliseconds);
	        	Console.WriteLine("End:  Nodes: " + tree3.CountNodes() + " Depth: " + tree3.Depth() );
	        	Console.WriteLine("Total Tree with  d=" + String.Format("{0,4:D}",d) + ":         "   +TimeSpan.FromTicks(ttree3-ttree0).TotalMilliseconds);
        	}
        	
        	
        	
        }

        
        static void PerfCheck(int toAdd)
        {
        	Console.WriteLine("Add Random " + toAdd);
        	int[]degrees=new int[] {13};
        	var rnd=new Random(0);
        	var sd=new SortedDictionary<int,int>();
        	int[] src=new int[toAdd];
        	for(int i=0;i<src.Length;i++)
        		src[i]=i;
        	for(int i=0;i<src.Length;i++)
        	{
        		int idx=rnd.Next(src.Length);
        		int h=src[i];
        		src[i]=src[idx];
        		src[idx]=h;
        	}

        	
        	
        	foreach( int d in degrees)
        	{
	        	var tree3=new BTreeSortedDictionary<int,int>(null,d,null);
	        	var ttree0=DateTime.Now.Ticks;
	        	for(int i=0;i<toAdd;i++)
	        		tree3.Add(src[i],-src[i]);
	        		        	
	        	var ttree1=DateTime.Now.Ticks;
	        	Console.WriteLine("Add Tree with    d=" + String.Format("{0,4:D}",d) + ": "   +TimeSpan.FromTicks(ttree1-ttree0).TotalMilliseconds);
				long sum=0;
	        	for(int i=0;i<toAdd;i++)
	        	{        		
	        		sum+=tree3[src[i]];;
	        	}
	        	var ttree2=DateTime.Now.Ticks;
	        	Console.WriteLine("Retr. Tree with  d=" + String.Format("{0,4:D}",d) + ": "   +TimeSpan.FromTicks(ttree2-ttree1).TotalMilliseconds);
	        	for(int i=0;i<toAdd;i++)
	        		tree3.Remove(src[i]);
	        	var ttree3=DateTime.Now.Ticks;
	        	Console.WriteLine("Remove Tree with d=" + String.Format("{0,4:D}",d) + ": "   +TimeSpan.FromTicks(ttree3-ttree2).TotalMilliseconds);
	        	Console.WriteLine("Total Tree with  d=" + String.Format("{0,4:D}",d) + ":         "   +TimeSpan.FromTicks(ttree3-ttree0).TotalMilliseconds);
        	}
        	
        	
        	
        }
        static void PerfCheckSD(int toAdd)
        {
        	Console.WriteLine("Add Random " + toAdd);
        	int[]degrees=new int[] {13};
        	var rnd=new Random(0);
        	int[] src=new int[toAdd];
        	for(int i=0;i<src.Length;i++)
        		src[i]=i;
        	for(int i=0;i<src.Length;i++)
        	{
        		int idx=rnd.Next(src.Length);
        		int h=src[i];
        		src[i]=src[idx];
        		src[idx]=h;
        	}

        	
        	
        	foreach( int d in degrees)
        	{
	        	var sd=new SortedDictionary<int,int>();
	        	var ttree0=DateTime.Now.Ticks;
	        	for(int i=0;i<toAdd;i++)
	        		sd.Add(src[i],-src[i]);
	        		        	
	        	var ttree1=DateTime.Now.Ticks;
	        	Console.WriteLine("Add  " + TimeSpan.FromTicks(ttree1-ttree0).TotalMilliseconds);
				long sum=0;
	        	for(int i=0;i<toAdd;i++)
	        	{        		
	        		sum+=sd[src[i]];;
	        	}
	        	var ttree2=DateTime.Now.Ticks;
	        	Console.WriteLine("Retr " + TimeSpan.FromTicks(ttree2-ttree1).TotalMilliseconds);
	        	for(int i=0;i<toAdd;i++)
	        		sd.Remove(src[i]);
	        	var ttree3=DateTime.Now.Ticks;
	        	Console.WriteLine("Rem =" +TimeSpan.FromTicks(ttree3-ttree2).TotalMilliseconds);
	        	Console.WriteLine("Total Tree with  d=" + String.Format("{0,4:D}",d) + ":         "   +TimeSpan.FromTicks(ttree3-ttree0).TotalMilliseconds);
        	}
        	
        	
        	
        }

        
        static void Main(string[] args)
        {
        	//PerfAddContainRemove(50000000);
        	
        	var tree = new BTreeSortedDictionary<int, int>(3,null);
            var master = new SortedDictionary<int, int>();
            var rnd = new Random(0);
            for (int i = 0; i < 100; i++)
            {
                Console.WriteLine("Iteration: " + i);
                Step(tree, master, 700000, rnd);
                Compare<int, int>(tree, master);
                Step(tree, master, 0, rnd);
                Compare<int, int>(tree, master);
                Step(tree, master, 350000, rnd);
                Compare<int, int>(tree, master);
                Step(tree, master, 2, rnd);
                Compare<int, int>(tree, master);
            }
            Console.WriteLine();
        }
    }
}
                                           