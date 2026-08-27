using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Xml;

namespace LPR381Solver
{
    public class KnapsackResults
    {
        public double GetOptimalValue{get;set;}
        public int[] BestCombination{get;set;}
        public string IterationLog {get;set;}
    }
    public class Item
    {
        public int OriginalIndex{get;set;}
        public double Profit {get;set;}
        public double Weight {get;set;}
        public double Ratio => Profit/Weight;
    }
    private class Item
    {
        public int OriginalIndex{get;set;}
        public double Profit {get;set;}
        public double Weight {get;set;}
        public double Ratio => Profit / Weight;
    }
    private class Node
    {
        public int NodeId{get;set;}
        public int Level {get;set;}
        public double Profit{get;set;}
        public double Weight{get;set;}
        public double Bound {get;set;}
        public List<int> SelectedItems{get;set;}

        public Node()
        {
            SelectedItems = new List<int>();
        }
    
    ///<summary>
    /// Solves the 0/1 Knapsack problem using Branch and bound .
    /// Accepts standard matrix format to Match simplex signatures .
    /// Assumes A[0] contains the weight and b[0] is the capacity.
    ///</summary>
    
    public static KnapsackResult solve(double[] c,double[,] A, double[] b)
        {
            int n = c.Length;
            double capacity = b[0];
            
            //1. Map input to Item objects and sort by Profit/Weight ratio descending 
            var items = new List<Item>();
            for (int i = 0; i < n; i++)
            {
                items.Add(new Item
                {
                    OriginalIndex = i,
                    Profit = c[i],
                    Weight = A[0,i]
                });
            }
            items = items.OrderByDescending(x => x.Ratio).ToList();

            //2. Setup Backtracking Stack and State Variables
            Stack<Node> stack = new Stack<Node>();
            double maxProfit = 0;
            List<int> bestItems = new List<int>();
            int nodeCounter = 0;

            StringBuilder iterationLog = new StringBuilder();
            iterationLog.AppendLine(string.Format("{0,-8} | {1,-6} | {2,-15} | {3,-8} | {4,-8} | {5,-8} | {6,-15}", 
                "Node ID", "Level", "Action", "Profit", "Weight", "Bound", "Status"));
                iterationLog.AppendLine(new string('-',85));

                Node root = new Node
                {
                    NodeId = ++nodeCounter,
                    Level = -1,
                    Profit = 0,
                    Weight = 0
                };
                root.Bound = CalculateBound(root, n, capacity, items);
                stack.Push(root);

                iterationLog.AppendLine(string.Format("{0,-8} | {1,-6} | {2,-15} | {3,-8:F2} | {4,-8:F2} | {5,-8:F2} | {6,-15}",
                    root.NodeId, "Root", "Initialize", root.Profit, root.Weight, root.Bound, "Active"));
                //3. Evaluate Sub-problems
                while(stack.Count > 0)
            {
                Node u = stack.Pop();
            //if this is the last level or the bound is worse then the current best
                if(u.Level == n - 1)continue;
            //Create Left Child
            Node leftChild = new Node
            {
                NodeID = ++nodeCounter,
                Level = u.Level + 1,
                Weight = u.Weight + items[u.Level + 1].Weight,
                Profit = u.Profit + items[u.Level + 1].Profit,
                SelectedItems = new List<int>(u.SelectedItems) 
            };
            leftChild.SelectedItems.Add(items[leftChild.Level].OriginalIndex);

            string leftStatus = "Active";
            if(leftChild.Weight <= capacity && leftChild.Profit > maxProfit)
                {
                    maxProfit = leftChild.Profit;
                    bestItems =new List<int>(leftChild.SelectedItems);
                }
                leftChild.Bound = CalculateBound(leftChild, n, capacity, items);

                if (leftChild.Weight > capacity) leftStatus = "Fathomed (Weight)";
                else if (leftChild.Bound <= maxProfit) leftStatus = "Fathomed (Bound)";
                else stack.Push(leftChild);

                iterationLog.AppendLine(string.Format("{0,-8} | {1,-6} | {2,-15} | {3,-8:F2} | {4,-8:F2} | {5,-8:F2} | {6,-15}",
                    leftChild.NodeId, leftChild.Level, $"Include x{items[leftChild.Level].OriginalIndex + 1}", 
                    leftChild.Profit, leftChild.Weight, leftChild.Bound, leftStatus));

                    //Create Right Child 
                    Node rightChild = new Node
                    {
                        NodeId = ++nodeCounter,
                    Level = u.Level + 1,
                    Weight = u.Weight,
                    Profit = u.Profit,
                    SelectedItems = new List<int>(u.SelectedItems)
                    };

                    rightChild.Bound = CancellationToken(rightChild, n, capacity, items);
                    string rightStatus = "Active";

                    if(rightChild.Bound <= maxProfit) rightStatus = "Fathomed (Bound)";
                    else stack.Push(rightChild);

                    iterationLog.AppendLine(string.Format("{0,-8} | {1,-6} | {2,-15} | {3,-8:F2} | {4,-8:F2} | {5,-8:F2} | {6,-15}",
                    rightChild.NodeId, rightChild.Level, $"Exclude x{items[rightChild.Level].OriginalIndex + 1}", 
                    rightChild.Profit, rightChild.Weight, rightChild.Bound, rightStatus));
            

            }
            //4. format Output
            int[] finalCombintion = new int[n];
            foreach (var index in bestItems)
            {
                finalCombintion[index] = 1;
            } 
            return new KnapsackResult
            {
                OptimalValue = maxProfit,
                BestCombination = finalCombintion,
                iterationLog = iterationLog.ToString(),
                NumEvaluationNodes = nodeCounter
            };
        }
        //Calculates the fractional knapsack upper bound for remaining capacity
        private static double CalculateBound(Node u,double capacity,List<Item> items)
        {
            if (u.Weight >= capacity) return 0;

            double profitBound = u.Profit;
            int j = u.Level + 1;
            double totWeight = u.Weight;

            while((j < n) && (totWeight + items[j].Weight <= capacity))
            {
                totWeight += items[j].Weight;
                profitBound += items[j].Profit;
                j++;
            }
            if(j < n)
            {
                profitBound += (capacity - totWeight) * items[j].Ratio;
            }
            return profitBound;
        }
    }    
}

