using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JiebaNet.Segmenter.Common;

namespace JiebaNet.Segmenter.PosSeg
{
    public class PosSegmenter
    {
        private static readonly WordDictionary WordDict = WordDictionary.Instance;
        private static readonly Viterbi PosSeg = Viterbi.Instance;

        // TODO: 
        private static readonly object locker = new object();

        #region Regular Expressions

        internal static readonly Regex RegexChineseInternal = new Regex(@"([\u4E00-\u9FD5a-zA-Z0-9+#&\._%·\-]+)", RegexOptions.Compiled);
        internal static readonly Regex RegexSkipInternal = new Regex(@"(\r\n|\s)", RegexOptions.Compiled);

        internal static readonly Regex RegexChineseDetail = new Regex(@"([\u4E00-\u9FD5]+)", RegexOptions.Compiled);
        internal static readonly Regex RegexSkipDetail = new Regex(@"([\.0-9]+|[a-zA-Z0-9]+)", RegexOptions.Compiled);

        internal static readonly Regex RegexEnglishWords = new Regex(@"[a-zA-Z0-9]+", RegexOptions.Compiled);
        internal static readonly Regex RegexNumbers = new Regex(@"[\.0-9]+", RegexOptions.Compiled);

        internal static readonly Regex RegexEnglishChar = new Regex(@"^[a-zA-Z0-9]$", RegexOptions.Compiled);

        #endregion

        private static IDictionary<string, string> _wordTagTab;

        static PosSegmenter()
        {
            LoadWordTagTab();
        }

        private static void LoadWordTagTab()
        {
            try
            {
                _wordTagTab = new Dictionary<string, string>();
                string[] lines = File.ReadAllLines(ConfigManager.MainDictFile, Encoding.UTF8);
                foreach (string line in lines)
                {
                    string[] tokens = line.Split(' ');
                    if (tokens.Length < 2)
                    {
                        Debug.Fail(string.Format("Invalid line: {0}", line));
                        continue;
                    }

                    string word = tokens[0];
                    string tag = tokens[2];

                    _wordTagTab[word] = tag;
                }
            }
            catch (IOException e)
            {
                Debug.Fail(string.Format("Word tag table load failure, reason: {0}", e.Message));
            }
            catch (FormatException fe)
            {
                Debug.Fail(fe.Message);
            }
        }

        private JiebaSegmenter _segmenter;

        public PosSegmenter()
        {
            _segmenter = new JiebaSegmenter();
        }

        public PosSegmenter(JiebaSegmenter segmenter)
        {
            _segmenter = segmenter;
        }

        private void CheckNewUserWordTags()
        {
            if (_segmenter.UserWordTagTab.IsNotEmpty())
            {
                _wordTagTab.Update(_segmenter.UserWordTagTab);
                _segmenter.UserWordTagTab = new Dictionary<string, string>();
            }
        }

        public IEnumerable<Pair> Cut(string text, bool hmm = true)
        {
            return CutInternal(text, hmm);
        }
        
        public IEnumerable<IEnumerable<Pair>> CutInParallel(IEnumerable<string> texts, bool hmm = true)
        {
            return texts.AsParallel().AsOrdered().Select(text => CutInternal(text, hmm));
        }
        
        public IEnumerable<IEnumerable<Pair>> CutInParallel(string text, bool hmm = true)
        {
            string[] lines = text.SplitLines();
            return CutInParallel(lines, hmm);
        }

        #region Internal Cut Methods

        internal IEnumerable<Pair> CutInternal(string text, bool hmm = true)
        {
            CheckNewUserWordTags();

            string[] blocks = RegexChineseInternal.Split(text);
            Func<string, IEnumerable<Pair>> cutMethod = null;
            if (hmm)
            {
                cutMethod = CutDag;
            }
            else
            {
                cutMethod = CutDagWithoutHmm;
            }

            List<Pair> tokens = new List<Pair>();
            foreach (string blk in blocks)
            {
                if (RegexChineseInternal.IsMatch(blk))
                {
                    tokens.AddRange(cutMethod(blk));
                }
                else
                {
                    string[] tmp = RegexSkipInternal.Split(blk);
                    foreach (string x in tmp)
                    {
                        if (RegexSkipInternal.IsMatch(x))
                        {
                            tokens.Add(new Pair(x, "x"));
                        }
                        else
                        {
                            foreach (char xx in x)
                            {
                                // TODO: each char?
                                string xxs = xx.ToString();
                                if (RegexNumbers.IsMatch(xxs))
                                {
                                    tokens.Add(new Pair(xxs, "m"));
                                }
                                else if (RegexEnglishWords.IsMatch(x))
                                {
                                    tokens.Add(new Pair(xxs, "eng"));
                                }
                                else
                                {
                                    tokens.Add(new Pair(xxs, "x"));
                                }
                            }
                        }
                    }
                }
            }

            return tokens;
        }

        internal IEnumerable<Pair> CutDag(string sentence)
        {
            IDictionary<int, List<int>> dag = _segmenter.GetDag(sentence);
            IDictionary<int, Pair<int>> route = _segmenter.Calc(sentence, dag);

            List<Pair> tokens = new List<Pair>();

            int x = 0;
            int n = sentence.Length;
            string buf = string.Empty;
            while (x < n)
            {
                int y = route[x].Key + 1;
                string w = sentence.Substring(x, y - x);
                if (y - x == 1)
                {
                    buf += w;
                }
                else
                {
                    if (buf.Length > 0)
                    {
                        AddBufferToWordList(tokens, buf);
                        buf = string.Empty;
                    }
                    tokens.Add(new Pair(w, _wordTagTab.GetDefault(w, "x")));
                }
                x = y;
            }

            if (buf.Length > 0)
            {
                AddBufferToWordList(tokens, buf);
            }

            return tokens;
        }

        internal IEnumerable<Pair> CutDagWithoutHmm(string sentence)
        {
            IDictionary<int, List<int>> dag = _segmenter.GetDag(sentence);
            IDictionary<int, Pair<int>> route = _segmenter.Calc(sentence, dag);

            List<Pair> tokens = new List<Pair>();

            int x = 0;
            string buf = string.Empty;
            int n = sentence.Length;

            int y = -1;
            while (x < n)
            {
                y = route[x].Key + 1;
                string w = sentence.Substring(x, y - x);
                // TODO: char or word?
                if (RegexEnglishChar.IsMatch(w))
                {
                    buf += w;
                    x = y;
                }
                else
                {
                    if (buf.Length > 0)
                    {
                        tokens.Add(new Pair(buf, "eng"));
                        buf = string.Empty;
                    }
                    tokens.Add(new Pair(w, _wordTagTab.GetDefault(w, "x")));
                    x = y;
                }
            }

            if (buf.Length > 0)
            {
                tokens.Add(new Pair(buf, "eng"));
            }

            return tokens;
        }

        internal IEnumerable<Pair> CutDetail(string text)
        {
            List<Pair> tokens = new List<Pair>();
            string[] blocks = RegexChineseDetail.Split(text);
            foreach (string blk in blocks)
            {
                if (RegexChineseDetail.IsMatch(blk))
                {
                    tokens.AddRange(PosSeg.Cut(blk));
                }
                else
                {
                    string[] tmp = RegexSkipDetail.Split(blk);
                    foreach (string x in tmp)
                    {
                        if (!string.IsNullOrWhiteSpace(x))
                        {
                            if (RegexNumbers.IsMatch(x))
                            {
                                tokens.Add(new Pair(x, "m"));
                            }
                            else if(RegexEnglishWords.IsMatch(x))
                            {
                                tokens.Add(new Pair(x, "eng"));
                            }
                            else
                            {
                                tokens.Add(new Pair(x, "x"));
                            }
                        }
                    }
                }
            }

            return tokens;
        }

        #endregion

        #region Private Helpers

        private void AddBufferToWordList(List<Pair> words, string buf)
        {
            if (buf.Length == 1)
            {
                words.Add(new Pair(buf, _wordTagTab.GetDefault(buf, "x")));
            }
            else
            {
                if (!WordDict.ContainsWord(buf))
                {
                    IEnumerable<Pair> tokens = CutDetail(buf);
                    words.AddRange(tokens);
                }
                else
                {
                    words.AddRange(buf.Select(ch => new Pair(ch.ToString(), "x")));
                }
            }
        }

        #endregion
    }
}