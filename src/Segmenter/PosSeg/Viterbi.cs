using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JiebaNet.Segmenter.Common;
using Newtonsoft.Json;

namespace JiebaNet.Segmenter.PosSeg
{
    public class Viterbi
    {
        private static readonly Lazy<Viterbi> Lazy = new Lazy<Viterbi>(() => new Viterbi());

        private static IDictionary<string, double> _startProbs;
        private static IDictionary<string, IDictionary<string, double>> _transProbs;
        private static IDictionary<string, IDictionary<char, double>> _emitProbs;
        private static IDictionary<char, List<string>> _stateTab;

        private Viterbi()
        {
            LoadModel();
        }

        // TODO: synchronized
        public static Viterbi Instance
        {
            get { return Lazy.Value; }
        }

        public IEnumerable<Pair> Cut(string sentence)
        {
            Tuple<double, List<string>> probPosList = ViterbiCut(sentence);
            List<string> posList = probPosList.Item2;

            List<Pair> tokens = new List<Pair>();
            int begin = 0, next = 0;
            for (int i = 0; i < sentence.Length; i++)
            {
                string[] parts = posList[i].Split('-');
                char charState = parts[0][0];
                string pos = parts[1];
                if (charState == 'B')
                    begin = i;
                else if (charState == 'E')
                {
                    tokens.Add(new Pair(sentence.Sub(begin, i + 1), pos));
                    next = i + 1;
                }
                else if (charState == 'S')
                {
                    tokens.Add(new Pair(sentence.Sub(i, i + 1), pos));
                    next = i + 1;
                }
            }
            if (next < sentence.Length)
            {
                tokens.Add(new Pair(sentence.Substring(next), posList[next].Split('-')[1]));
            }
            
            return tokens;
        }

        #region Private Helpers

        private static void LoadModel()
        {
            string startJson = File.ReadAllText(Path.GetFullPath(ConfigManager.PosProbStartFile));
            _startProbs = JsonConvert.DeserializeObject<IDictionary<string, double>>(startJson);

            string transJson = File.ReadAllText(Path.GetFullPath(ConfigManager.PosProbTransFile));
            _transProbs = JsonConvert.DeserializeObject<IDictionary<string, IDictionary<string, double>>>(transJson);

            string emitJson = File.ReadAllText(Path.GetFullPath(ConfigManager.PosProbEmitFile));
            _emitProbs = JsonConvert.DeserializeObject<IDictionary<string, IDictionary<char, double>>>(emitJson);

            string tabJson = File.ReadAllText(Path.GetFullPath(ConfigManager.CharStateTabFile));
            _stateTab = JsonConvert.DeserializeObject<IDictionary<char, List<string>>>(tabJson);
        }

        // TODO: change sentence to obs?
        private Tuple<double, List<string>> ViterbiCut(string sentence)
        {
            List<IDictionary<string, double>> v = new List<IDictionary<string, double>>();
            List<IDictionary<string, string>> memPath = new List<IDictionary<string, string>>();

            List<string> allStates = _transProbs.Keys.ToList();

            // Init weights and paths.
            v.Add(new Dictionary<string, Double>());
            memPath.Add(new Dictionary<string, string>());
            foreach (string state in _stateTab.GetDefault(sentence[0], allStates))
            {
                double emP = _emitProbs[state].GetDefault(sentence[0], Constants.MinProb);
                v[0][state] = _startProbs[state] + emP;
                memPath[0][state] = string.Empty;
            }

            // For each remaining char
            for (int i = 1; i < sentence.Length; ++i)
            {
                v.Add(new Dictionary<string, double>());
                memPath.Add(new Dictionary<string, string>());

                IEnumerable<string> prevStates = memPath[i - 1].Keys.Where(k => _transProbs[k].Count > 0);
                HashSet<string> curPossibleStates = new HashSet<string>(prevStates.SelectMany(s => _transProbs[s].Keys));

                IEnumerable<string> obsStates = _stateTab.GetDefault(sentence[i], allStates);
                obsStates = curPossibleStates.Intersect(obsStates);

                if (!obsStates.Any())
                {
                    if (curPossibleStates.Count > 0)
                    {
                        obsStates = curPossibleStates;
                    }
                    else
                    {
                        obsStates = allStates;
                    }
                }

                foreach (string y in obsStates)
                {
                    double emp = _emitProbs[y].GetDefault(sentence[i], Constants.MinProb);

                    double prob = double.MinValue;
                    string state = string.Empty;

                    foreach (string y0 in prevStates)
                    {
                        double tranp = _transProbs[y0].GetDefault(y, double.MinValue);
                        tranp = v[i - 1][y0] + tranp + emp;
                        // TODO: compare two very small values;
                        // TODO: how to deal with negative infinity
                        if (prob < tranp ||
                            (prob == tranp && string.Compare(state, y0, StringComparison.InvariantCulture) < 0))
                        {
                            prob = tranp;
                            state = y0;
                        }
                    }
                    v[i][y] = prob;
                    memPath[i][y] = state;
                }
            }

            IDictionary<string, double> vLast = v.Last();
            var last = memPath.Last().Keys.Select(y => new {State = y, Prob = vLast[y]});
            double endProb = double.MinValue;
            string endState = string.Empty;
            foreach (var endPoint in last)
            {
                // TODO: compare two very small values;
                if (endProb < endPoint.Prob || 
                    (endProb == endPoint.Prob && String.Compare(endState, endPoint.State, StringComparison.InvariantCulture) < 0))
                {
                    endProb = endPoint.Prob;
                    endState = endPoint.State;
                }
            }

            string[] route = new string[sentence.Length];
            int n = sentence.Length - 1;
            string curState = endState;
            while(n >= 0)
            {
                route[n] = curState;
                curState = memPath[n][curState];
                n--;
            }

            return new Tuple<double, List<string>>(endProb, route.ToList());
        }

        #endregion
    }
}