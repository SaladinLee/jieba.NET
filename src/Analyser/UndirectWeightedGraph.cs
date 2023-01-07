using System;
using System.Collections.Generic;
using System.Linq;

namespace JiebaNet.Analyser
{
    public class Edge
    {
        public string Start { get; set; }
        public string End { get; set; }
        public double Weight { get; set; }
    }

    public class UndirectWeightedGraph
    {
        private static readonly double d = 0.85;

        public IDictionary<string, List<Edge>> Graph { get; set; } 
        public UndirectWeightedGraph()
        {
            Graph = new Dictionary<string, List<Edge>>();
        }

        public void AddEdge(string start, string end, double weight)
        {
            if (!Graph.ContainsKey(start))
            {
                Graph[start] = new List<Edge>();
            }

            if (!Graph.ContainsKey(end))
            {
                Graph[end] = new List<Edge>();
            }

            Graph[start].Add(new Edge(){ Start = start, End = end, Weight = weight });
            Graph[end].Add(new Edge(){ Start = end, End = start, Weight = weight });
        }

        public IDictionary<string, double> Rank()
        {
            Dictionary<string, double> ws = new Dictionary<string, double>();
            Dictionary<string, double> outSum = new Dictionary<string, double>();

            // init scores
            int count = Graph.Count > 0 ? Graph.Count : 1;
            double wsdef = 1.0/count;

            foreach (KeyValuePair<string, List<Edge>> pair in Graph)
            {
                ws[pair.Key] = wsdef;
                outSum[pair.Key] = pair.Value.Sum(e => e.Weight);
            }

            // TODO: 10 iterations?
            IOrderedEnumerable<string> sortedKeys = Graph.Keys.OrderBy(k => k);
            for (int i = 0; i < 10; i++)
            {
                foreach (string n in sortedKeys)
                {
                    double s = 0d;
                    foreach (Edge edge in Graph[n])
                    {
                        s += edge.Weight/outSum[edge.End]*ws[edge.End];
                    }
                    ws[n] = (1 - d) + d*s;
                }
            }

            double minRank = double.MaxValue;
            double maxRank = double.MinValue;

            foreach (double w in ws.Values)
            {
                if (w < minRank)
                {
                    minRank = w;
                }
                if(w > maxRank)
                {
                    maxRank = w;
                }
            }

            foreach (KeyValuePair<string, double> pair in ws.ToList())
            {
                ws[pair.Key] = (pair.Value - minRank/10.0)/(maxRank - minRank/10.0);
            }

            return ws;
        }
    }
}