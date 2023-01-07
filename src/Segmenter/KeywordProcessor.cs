using System.Collections.Generic;
using System.Linq;
using JiebaNet.Segmenter.Common;

namespace JiebaNet.Segmenter
{
    public class KeywordProcessor
    {
        // private readonly string _keyword = "_keyword_";
        // private readonly ISet<char> _whiteSpaceChars = new HashSet<char>(".\t\n\a ,");
        // private readonly bool CaseSensitive;
        private readonly KeywordTrie KeywordTrie = new KeywordTrie();

        private readonly ISet<char> NonWordBoundries =
            new HashSet<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_");

        public bool CaseSensitive { get; }

        public KeywordProcessor(bool caseSensitive = false)
        {
            CaseSensitive = caseSensitive;
        }
        
        public void AddKeyword(string keyword, string cleanName = null)
        {
            SetItem(keyword, cleanName);
        }

        public void AddKeywords(IEnumerable<string> keywords)
        {
            foreach (string keyword in keywords)
            {
                AddKeyword(keyword);
            }
        }

        public void RemoveKeyword(string keyword)
        {
            if (!CaseSensitive)
            {
                keyword = keyword.ToLower();
            }
            KeywordTrie.Remove(keyword);
        }
        
        public void RemoveKeywords(IEnumerable<string> keywords)
        {
            foreach (string keyword in keywords)
            {
                RemoveKeyword(keyword);
            }
        }

        public bool Contains(string word)
        {
            return GetItem(word).IsNotNull();
        }

        public IEnumerable<TextSpan> ExtractKeywordSpans(string sentence)
        {
            List<TextSpan> keywordsExtracted = new List<TextSpan>();
            if (sentence.IsEmpty())
            {
                return keywordsExtracted;
            }

            if (!CaseSensitive)
            {
                sentence = sentence.ToLower();
            }

            KeywordTrieNode currentState = KeywordTrie;
            int seqStartPos = 0;
            int seqEndPos = 0;
            bool resetCurrentDict = false;
            int idx = 0;
            int sentLen = sentence.Length;
            while (idx < sentLen)
            {
                char ch = sentence[idx];
                // when reaching a char that denote word end
                if (!NonWordBoundries.Contains(ch))
                {
                    // if current prefix is in trie
                    if (currentState.HasValue || currentState.HasChild(ch))
                    {
                        //string seqFound = null;
                        string longestFound = null;
                        bool isLongerFound = false;
                        
                        if (currentState.HasValue)
                        {
                            //seqFound = currentState.Value;
                            longestFound = currentState.Value;
                            seqEndPos = idx;
                        }

                        // re look for longest seq from this position
                        if (currentState.HasChild(ch))
                        {
                            KeywordTrieNode curStateContinued = currentState.GetChild(ch);
                            int idy = idx + 1;
                            while (idy < sentLen)
                            {
                                char innerCh = sentence[idy];
                                if (!NonWordBoundries.Contains(innerCh) && curStateContinued.HasValue)
                                {
                                    longestFound = curStateContinued.Value;
                                    seqEndPos = idy;
                                    isLongerFound = true;
                                }

                                if(curStateContinued.HasChild(innerCh))
                                {
                                    curStateContinued = curStateContinued.GetChild(innerCh);
                                }
                                else
                                {
                                    break;
                                }

                                idy += 1;
                            }

                            if (idy == sentLen && curStateContinued.HasValue)
                            {
                                // end of sentence reached.
                                longestFound = curStateContinued.Value;
                                seqEndPos = idy;
                                isLongerFound = true;
                            }

                            if (isLongerFound)
                            {
                                idx = seqEndPos;
                            }
                        }
                        
                        if (longestFound.IsNotEmpty())
                        {
                            keywordsExtracted.Add(new TextSpan(text: longestFound, start: seqStartPos, end: idx));
                        }

                        currentState = KeywordTrie;
                        resetCurrentDict = true;
                    }
                    else
                    {
                        currentState = KeywordTrie;
                        resetCurrentDict = true;
                    }
                }
                else if (currentState.HasChild(ch))
                {
                    currentState = currentState.GetChild(ch);
                }
                else
                {
                    currentState = KeywordTrie;
                    resetCurrentDict = true;

                    // skip to end of word
                    int idy = idx + 1;
                    while (idy < sentLen)
                    {
                        if (!NonWordBoundries.Contains(sentence[idy]))
                        {
                            break;
                        }
                        idy += 1;
                    }

                    idx = idy;
                }

                if (idx + 1 >= sentLen)
                {
                    if (currentState.HasValue)
                    {
                        string seqFound = currentState.Value;
                        keywordsExtracted.Add(new TextSpan(text: seqFound, start: seqStartPos, end: sentLen));
                    }
                }

                idx += 1;
                if (resetCurrentDict)
                {
                    resetCurrentDict = false;
                    seqStartPos = idx;
                }
            }

            return keywordsExtracted;
        }

        public IEnumerable<string> ExtractKeywords(string sentence, bool raw = false)
        {
            if (raw)
            {
                return ExtractKeywordSpans(sentence).Select(span => sentence.Sub(span.Start, span.End));
            }
            
            return ExtractKeywordSpans(sentence).Select(span => span.Text);
        }

        #region Private methods

        private void SetItem(string keyword, string cleanName)
        {
            if (cleanName.IsEmpty() && keyword.IsNotEmpty())
            {
                cleanName = keyword;
            }

            if (keyword.IsNotEmpty() && cleanName.IsNotEmpty())
            {
                if (!CaseSensitive)
                {
                    keyword = keyword.ToLower();
                }

                KeywordTrie[keyword] = cleanName;
            }
        }
        
        private string GetItem(string word)
        {
            if (!CaseSensitive)
            {
                word = word.ToLower();
            }

            return KeywordTrie[word];
        }

        #endregion
    }
}