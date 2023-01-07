using System.Collections.Generic;
using System.Linq;
using JiebaNet.Segmenter;
using JiebaNet.Segmenter.Common;
using JiebaNet.Segmenter.PosSeg;

namespace JiebaNet.Analyser
{
    public class TextRankExtractor : KeywordExtractor
    {
        private static readonly IEnumerable<string> DefaultPosFilter = new List<string>()
        {
            "n", "ng", "nr", "nrfg", "nrt", "ns", "nt", "nz", "v", "vd", "vg", "vi", "vn", "vq"
        };

        private JiebaSegmenter Segmenter { get; set; }
        private PosSegmenter PosSegmenter { get; set; }

        public int Span { get; set; }

        public bool PairFilter(IEnumerable<string> allowPos, Pair wp)
        {
            return allowPos.Contains(wp.Flag)
                   && wp.Word.Trim().Length >= 2
                   && !StopWords.Contains(wp.Word.ToLower());
        }

        public TextRankExtractor()
        {
            Span = 5;

            Segmenter = new JiebaSegmenter();
            PosSegmenter = new PosSegmenter(Segmenter);
            SetStopWords(ConfigManager.StopWordsFile);
            if (StopWords.IsEmpty())
            {
                StopWords.UnionWith(DefaultStopWords);
            }
        }

        public override IEnumerable<string> ExtractTags(string text, int count = 20, IEnumerable<string> allowPos = null)
        {
            IDictionary<string, double> rank = ExtractTagRank(text, allowPos);
            if (count <= 0) { count = 20; }
            return rank.OrderByDescending(p => p.Value).Select(p => p.Key).Take(count);
        }

        public override IEnumerable<WordWeightPair> ExtractTagsWithWeight(string text, int count = 20, IEnumerable<string> allowPos = null)
        {
            IDictionary<string, double> rank = ExtractTagRank(text, allowPos);
            if (count <= 0) { count = 20; }
            return rank.OrderByDescending(p => p.Value).Select(p => new WordWeightPair()
            {
                Word = p.Key, Weight = p.Value
            }).Take(count);
        }

        #region Private Helpers

        private IDictionary<string, double> ExtractTagRank(string text, IEnumerable<string> allowPos)
        {
            if (allowPos.IsEmpty())
            {
                allowPos = DefaultPosFilter;
            }

            UndirectWeightedGraph g = new UndirectWeightedGraph();
            Dictionary<string, int> cm = new Dictionary<string, int>();
            List<Pair> words = PosSegmenter.Cut(text).ToList();

            for (int i = 0; i < words.Count(); i++)
            {
                Pair wp = words[i];
                if (PairFilter(allowPos, wp))
                {
                    for (int j = i + 1; j < i + Span; j++)
                    {
                        if (j >= words.Count)
                        {
                            break;
                        }
                        if (!PairFilter(allowPos, words[j]))
                        {
                            continue;
                        }

                        // TODO: better separator.
                        string key = wp.Word + "$" + words[j].Word;
                        if (!cm.ContainsKey(key))
                        {
                            cm[key] = 0;
                        }
                        cm[key] += 1;
                    }
                }
            }

            foreach (KeyValuePair<string, int> p in cm)
            {
                string[] terms = p.Key.Split('$');
                g.AddEdge(terms[0], terms[1], p.Value);
            }

            return g.Rank();
        }

        #endregion
    }
}