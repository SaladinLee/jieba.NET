using System.Collections.Generic;
using System.Linq;
using JiebaNet.Segmenter.Common;

namespace JiebaNet.Segmenter.Spelling
{
    public interface ISpellChecker
    {
        IEnumerable<string> Suggests(string word);
    }

    public class SpellChecker : ISpellChecker
    {
        internal static readonly WordDictionary WordDict = WordDictionary.Instance;

        internal readonly Trie WordTrie;
        internal readonly Dictionary<char, HashSet<char>> FirstChars; 

        public SpellChecker()
        {
            WordDictionary wordDict = WordDictionary.Instance;
            WordTrie = new Trie();
            FirstChars = new Dictionary<char, HashSet<char>>();

            foreach (KeyValuePair<string, int> wd in wordDict.Trie)
            {
                if (wd.Value > 0)
                {
                    WordTrie.Insert(wd.Key, wd.Value);

                    if (wd.Key.Length >= 2)
                    {
                        char second = wd.Key[1];
                        char first = wd.Key[0];
                        if (!FirstChars.ContainsKey(second))
                        {
                            FirstChars[second] = new HashSet<char>();
                        }
                        FirstChars[second].Add(first);
                    }
                }
            }
        }

        internal ISet<string> GetEdits1(string word)
        {
            List<WordSplit> splits = new List<WordSplit>();
            for (int i = 0; i <= word.Length; i++)
            {
                splits.Add(new WordSplit() { Left = word.Substring(0, i), Right = word.Substring(i) });
            }

            IEnumerable<string> deletes = splits
                .Where(s => !string.IsNullOrEmpty(s.Right))
                .Select(s => s.Left + s.Right.Substring(1));

            IEnumerable<string> transposes = splits
                .Where(s => s.Right.Length > 1)
                .Select(s => s.Left + s.Right[1] + s.Right[0] + s.Right.Substring(2));

            HashSet<string> replaces = new HashSet<string>();
            if (word.Length > 1)
            {
                HashSet<char> firsts = FirstChars[word[1]];
                foreach (char first in firsts)
                {
                    if (first != word[0])
                    {
                        replaces.Add(first + word.Substring(1));
                    }
                }

                TrieNode node = WordTrie.Root.Children[word[0]];
                for (int i = 1; node.IsNotNull() && node.Children.IsNotEmpty() && i < word.Length; i++)
                {
                    foreach (char c in node.Children.Keys)
                    {
                        replaces.Add(word.Substring(0, i) + c + word.Substring(i + 1));
                    }
                    node = node.Children.GetOrDefault(word[i]);
                }
            }

            HashSet<string> inserts = new HashSet<string>();
            if (word.Length > 1)
            {
                if (FirstChars.ContainsKey(word[0]))
                {
                    HashSet<char> firsts = FirstChars[word[0]];
                    foreach (char first in firsts)
                    {
                        inserts.Add(first + word);
                    }
                }

                TrieNode node = WordTrie.Root.Children.GetOrDefault(word[0]);
                for (int i = 0; node.IsNotNull() && node.Children.IsNotEmpty() && i < word.Length; i++)
                {
                    foreach (char c in node.Children.Keys)
                    {
                        inserts.Add(word.Substring(0, i+1) + c + word.Substring(i+1));
                    }

                    if (i < word.Length - 1)
                    {
                        node = node.Children.GetOrDefault(word[i + 1]);
                    }
                }
            }

            HashSet<string> result = new HashSet<string>();
            result.UnionWith(deletes);
            result.UnionWith(transposes);
            result.UnionWith(replaces);
            result.UnionWith(inserts);

            return result;
        }

        internal ISet<string> GetKnownEdits2(string word)
        {
            HashSet<string> result = new HashSet<string>();
            foreach (string e1 in GetEdits1(word))
            {
                result.UnionWith(GetEdits1(e1).Where(e => WordDictionary.Instance.ContainsWord(e)));
            }
            return result;
        }

        internal ISet<string> GetKnownWords(IEnumerable<string> words)
        {
            return new HashSet<string>(words.Where(w => WordDictionary.Instance.ContainsWord(w)));
        }

        public IEnumerable<string> Suggests(string word)
        {
            if (WordDict.ContainsWord(word))
            {
                return new[] {word};
            }

            ISet<string> candicates = GetKnownWords(GetEdits1(word));
            if (candicates.IsNotEmpty())
            {
                return candicates.OrderByDescending(c => WordDict.GetFreqOrDefault(c));
            }
            
            candicates.UnionWith(GetKnownEdits2(word));
            return candicates.OrderByDescending(c => WordDict.GetFreqOrDefault(c));
        }
    }

    internal class WordSplit
    {
        public string Left { get; set; }
        public string Right { get; set; }
    }
}
