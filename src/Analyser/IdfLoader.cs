using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JiebaNet.Analyser
{
    public class IdfLoader
    {
        internal string IdfFilePath { get; set; }
        internal IDictionary<string, double> IdfFreq { get; set; }
        internal double MedianIdf { get; set; }

        public IdfLoader(string idfPath = null)
        {
            IdfFilePath = string.Empty;
            IdfFreq = new Dictionary<string, double>();
            MedianIdf = 0.0;
            if (!string.IsNullOrWhiteSpace(idfPath))
            {
                SetNewPath(idfPath);
            }
        }

        public void SetNewPath(string newIdfPath)
        {
            string idfPath = Path.GetFullPath(newIdfPath);
            if (IdfFilePath != idfPath)
            {
                IdfFilePath = idfPath;
                string[] lines = File.ReadAllLines(idfPath, Encoding.UTF8);
                IdfFreq = new Dictionary<string, double>();
                foreach (string line in lines)
                {
                    string[] parts = line.Trim().Split(' ');
                    string word = parts[0];
                    double freq = double.Parse(parts[1]);
                    IdfFreq[word] = freq;
                }

                MedianIdf = IdfFreq.Values.OrderBy(v => v).ToList()[IdfFreq.Count / 2];
            }
        }
    }
}