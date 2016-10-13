using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace DataStructures.SimpleTrie
{
    [DataContract]
    [KnownType(typeof(WebTrie))]
    public class WebTrie : SimpleTrie
    {

        public WebTrie() : base()
        {

        }

        public WebTrie Clone() {
            WebTrie trie = new WebTrie();
            trie.root = root;
            return trie;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void InsertURL(string value)
        {
            //Splits the string by directory
            value = removeHeader(value);
            string[] valueSplit = value.Split(new char[] { '/', '&' }, StringSplitOptions.RemoveEmptyEntries);
            TrieNode node = base.insertVal(valueSplit[0]);
            int Length = valueSplit.Length - 1;
            for (int i = 0; i < Length; i++)
            {
                Dictionary<string, TrieNode> children = node.children;
                string val = valueSplit[i + 1];
                if (children.ContainsKey(val))
                {
                    node = children[val];
                }
                else
                {
                    node = new TrieNode(val);
                    children.Add(val, node);
                }
                if (i == Length - 1)
                {
                    node.isleaf = true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override bool contains(string value)
        {
            TrieNode node = search(value);
            return (node != null && node.isleaf);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override TrieNode search(string value)
        {
            value = removeHeader(value);
            string[] valueSplit = value.Split(new char[] { '/', '&' }, StringSplitOptions.RemoveEmptyEntries);
            //Early cancellation should the site not be in the baseTrie
            TrieNode node = base.search(valueSplit[0]);
            if (!(node != null && node.isleaf))
            {
                return null;
            }

            Dictionary<String, TrieNode> children = node.children;
            int Length = valueSplit.Length - 1;
            for (int i = 0; i < Length; i++)
            {
                string val = valueSplit[i + 1];
                if (children.ContainsKey(val))
                {
                    node = children[val];
                    children = node.children;
                }
                else
                {
                    return null;
                }
            }
            return node;
        }

        //Removes http headers from links
        public string removeHeader(string input)
        {
            string returnedstring = Regex.Replace(input, "https?://", "");
            return returnedstring;
        }
    }

}
