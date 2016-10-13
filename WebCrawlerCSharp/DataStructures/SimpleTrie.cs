using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;

namespace DataStructures.SimpleTrie
{
    [DataContract]
    [KnownType(typeof(SimpleTrie))]
    public class SimpleTrie
    {
        [DataMember]
        protected TrieNode root;

        public SimpleTrie()
        {
            root = new TrieNode();
        }

        public SimpleTrie Clone() {
            SimpleTrie trie = new SimpleTrie();
            trie.root = root;
            return trie;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected virtual TrieNode insertVal(string value)
        {
            TrieNode node = root;
            int Length = value.Length;
            for (int i = 0; i < Length; i++)
            {
                Dictionary<String, TrieNode> children = node.children;
                char val = value[i];
                if (children.ContainsKey(val.ToString()))
                {
                    node = children[val.ToString()];
                }
                else
                {
                    node = new TrieNode(val.ToString());
                    children.Add(val.ToString(), node);
                }
                if (i == Length - 1)
                {
                    node.isleaf = true;
                }
            }
            return node;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual bool contains(string value)
        {
            TrieNode node = search(value);
            return (node != null && node.isleaf);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual TrieNode search(string value)
        {
            Dictionary<string, TrieNode> children = root.children;
            TrieNode node = null;
            for (int i = 0; i < value.Length; i++)
            {
                char val = value[i];
                if (children.ContainsKey(val.ToString()))
                {
                    node = children[val.ToString()];
                    children = node.children;
                }
                else
                {
                    return null;
                }
            }
            return node;
        }

        //Prints the contents of the trie
        public void printAll()
        {
            printBelow(root, "");
        }

        void printBelow(TrieNode node, string prefix)
        {
            if (node != null)
            {
                prefix += node.val;
                if (node.isleaf)
                {
                    Console.WriteLine(prefix);
                }
                foreach (TrieNode child in node.children.Values)
                {
                    printBelow(child, prefix);
                }
            }
        }
    }

    [DataContract]
    [KnownType(typeof(TrieNode))]
    public class TrieNode
    {
        [DataMember]
        public string val { get; set; }
        [DataMember]
        public Dictionary<string, TrieNode> children { get; set; }
        [DataMember]
        public bool isleaf { get; set; }

        public TrieNode()
        {
            children = new Dictionary<string, TrieNode>();
        }

        public TrieNode(string val)
        {
            this.val = val;
            children = new Dictionary<string, TrieNode>();
        }
    }

}
