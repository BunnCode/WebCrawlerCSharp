using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

namespace DataAnalyzation.Markov.DataAnalyzation
{
    [DataContract]
    [KnownType(typeof(MarkovChain))]
    public class MarkovChain
    {
        // Hashmap
        [DataMember]
        private Dictionary<string, ArrayList> markovChain = new Dictionary<string, ArrayList>();
        [DataMember]
        private Random rnd = new Random();
        [DataMember]
        private int prefixSize;

        public MarkovChain(int prefixSize)
        {
            markovChain.Add("_start", new ArrayList());
            markovChain.Add("_end", new ArrayList());
            this.prefixSize = prefixSize;
        }

        public void addWords(string words)
        {
            string[] sentences = Regex.Split(words, "[.?!]");
            for (int i = 0; i < sentences.Length; i++)
            {
                string sentence = sentences[i];
                addSentence(Regex.Replace(sentence.Trim(), "[^A-Za-z ]", "") + '.');
            }
        }

        private void addSentence(string phrase)
        {
            // put each word into an array

            // Loop through each word, check if it's already added
            // if its added, then get the suffix vector and add the word
            // if it hasn't been added then add the word to the list
            // if its the first or last word then select the _start / _end key
            String[] words = Regex.Split(phrase, " ");
            if (words.Length < 1)
            {
                return;
            }
            
            for (int i = 0; i < words.Length; i++)
            {
                string prefix;
                StringBuilder prefixBuilder = new StringBuilder();
                for (int u = 0; u < prefixSize; u++)
                {
                    if (i + u > words.Length - 1)
                    {
                        goto CONTINUEGENERATEWORDS;
                    }
                    prefixBuilder.Append(words[i + u]).Append(" ");
                }
                prefix = prefixBuilder.ToString().ToLower().Trim();
                //Bounce null prefixes
                if (prefix.Trim().Equals("") || prefix.Trim().Equals("."))
                {
                    continue;
                }
                // Add the start and end words for locus of start and end points
                if (i == 0)
                {
                    ArrayList startWords = markovChain["_start"];
                    if (Regex.Split(prefix, " ").Length == prefixSize)
                    {
                        startWords.Add(prefix.Trim());
                    }
                }
                else if (i == words.Length - 1)
                {
                    ArrayList endWords = markovChain["_end"];
                    if (Regex.Split(prefix.Trim(), " ").Length == prefixSize)
                    {
                        endWords.Add(words[i]);
                    }
                }
                ArrayList suffix;
                //Adds the next word to the suffix pool of the prefixes
                if (!markovChain.TryGetValue(prefix, out suffix))
                {
                    suffix = new ArrayList();
                }

                if (i + prefixSize < words.Length)
                {
                    suffix.Add(words[i + prefixSize].Trim());
                }
                if (Regex.Split(prefix.Trim(), " ").Length == prefixSize) {
                    if (markovChain.ContainsKey(prefix)) {
                        markovChain[prefix] = suffix;
                    } else {
                        markovChain.Add(prefix, suffix);
                    }
                }
                CONTINUEGENERATEWORDS:;
            }
        }

        /*
         * Generate a markov phrase
         */
        public void generateSentence(int sentences)
        {

            // Vector to hold the phrase
            ArrayList newPhrase = new ArrayList();

            // string for the next word
            string nextWord = "";

            // Select the first word
            ArrayList startWords = markovChain["_start"];
            int startWordsLen = startWords.Count;
            while (nextWord == null || nextWord.Length == 0)
            {
                nextWord = (string)startWords[rnd.Next(startWordsLen)];
            }
            newPhrase.Add(nextWord);
            ArrayList previousWords = new ArrayList(nextWord.Split(' ').ToArray());
            previousWords.Reverse();
            previousWords.Capacity = prefixSize;
            string prefix;
            // Keep looping through the words until all sentences are constructed
            for (int i = 0; i < sentences;)
            {
                previousWords = new ArrayList(previousWords.GetRange(0, prefixSize));
                StringBuilder prefixBuilder = new StringBuilder();
                for (int u = previousWords.Count - 1; u >= 0; u--)
                {
                    prefixBuilder.Append(previousWords[u]).Append(" ");
                }
                prefix = prefixBuilder.ToString().Trim();
                //Attempt to get value
                ArrayList wordSelection = null;
                markovChain.TryGetValue(prefix.ToLower(), out wordSelection);
                //Roll again if it can't 
                if (wordSelection == null || wordSelection.Count == 0)
                {
                    //Keep rolling while it can't
                    while (nextWord == null || nextWord.Length == 0
                            || wordSelection == null || wordSelection.Count < 1)
                    {
                        nextWord = (string)startWords[rnd.Next(startWordsLen)];
                        //Try to get a value from the chain
                        markovChain.TryGetValue(nextWord.ToLower(), out wordSelection);
                    }
                    //Adds the new sentence beginning to the new phrase
                    newPhrase.Add(nextWord);
                    previousWords = new ArrayList(nextWord.Split(' ').ToArray());
                    previousWords.Reverse();
                    continue;
                }

                int wordSelectionLen = wordSelection.Count;
                nextWord = (string)wordSelection[rnd.Next(wordSelectionLen)];
                previousWords.Insert(0, nextWord);

                int Length = nextWord.Length;
                if (Length > 0)
                {
                    newPhrase.Add(nextWord);
                    if (nextWord[Length - 1] == '.')
                    {
                        i++;
                    }
                }
            }
            StringBuilder outputBuilder = new StringBuilder();
            for (int i = 0; i < newPhrase.Count; i++)
            {
                string word = (string)newPhrase[i];
                outputBuilder.Append(word).Append(" ");
            }
            Console.WriteLine(outputBuilder.ToString());
        }

        public int getPrefixSize()
        {
            return prefixSize;
        }
    }
}
