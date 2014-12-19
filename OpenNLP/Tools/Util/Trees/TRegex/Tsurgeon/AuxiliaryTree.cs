﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenNLP.Tools.Util.Trees.TRegex.Tsurgeon
{
    public class AuxiliaryTree
    {
        private readonly String originalTreeString;
  readonly Tree tree;
  Tree foot;
  private readonly IdentityDictionary<Tree,String> nodesToNames; // no one else should be able to get this one.
  private readonly Dictionary<String,Tree> namesToNodes; // this one has a getter.


  public AuxiliaryTree(Tree tree, bool mustHaveFoot) {
    originalTreeString = tree.ToString();
    this.tree = tree;
    this.foot = findFootNode(tree);
    if (foot == null && mustHaveFoot) {
      throw new TsurgeonParseException("Error -- no foot node found for " + originalTreeString);
    }
      namesToNodes = new Dictionary<String, Tree>();
    nodesToNames = new IdentityDictionary<Tree, string>();
    initializeNamesNodesMaps(tree);
  }

  private AuxiliaryTree(Tree tree, Tree foot, Dictionary<String, Tree> namesToNodes, String originalTreeString) {
    this.originalTreeString = originalTreeString;
    this.tree = tree;
    this.foot = foot;
    this.namesToNodes = namesToNodes;
    nodesToNames = null;
  }

  public Dictionary<String, Tree> namesToNodes() {
    return namesToNodes;
  }

  //@Override
  public override String ToString() {
    return originalTreeString;
  }

  /**
   * Copies the Auxiliary tree.  Also, puts the new names->nodes map in the TsurgeonMatcher that called copy.
   */
  public AuxiliaryTree copy(TsurgeonMatcher matcher) {
    Dictionary<String,Tree> newNamesToNodes = new Dictionary<string, Tree>();
    Tuple<Tree,Tree> result = copyHelper(tree,newNamesToNodes);
    //if(! result.Item1.dominates(result.Item2))
      //System.err.println("Error -- aux tree copy doesn't dominate foot copy.");
    foreach (var entry in newNamesToNodes)
    {
        matcher.newNodeNames.Add(entry.Key, entry.Value);
    }
    return new AuxiliaryTree(result.Item1, result.Item2, newNamesToNodes, originalTreeString);
  }

  // returns Pair<node,foot>
  private Tuple<Tree,Tree> copyHelper(Tree node,Dictionary<String,Tree> newNamesToNodes) {
    Tree clone;
    Tree newFoot = null;
    if (node.isLeaf()) {
      if (node == foot) { // found the foot node; pass it up.
        clone = node.treeFactory().newTreeNode(node.label(),new List<Tree>(0));
        newFoot = clone;
      } else {
        clone = node.treeFactory().newLeaf(node.label().labelFactory().newLabel(node.label()));
      }
    } else {
      List<Tree> newChildren = new List<Tree>(node.children().Length);
      foreach (Tree child in node.children()) {
        Tuple<Tree,Tree> newChild = copyHelper(child,newNamesToNodes);
        newChildren.Add(newChild.Item1);
        if (newChild.Item2 != null) {
          if (newFoot != null) {
            //System.err.println("Error -- two feet found when copying auxiliary tree " + tree.toString() + "; using last foot found.");
          }
          newFoot = newChild.Item2;
        }
      }
      clone = node.treeFactory().newTreeNode(node.label().labelFactory().newLabel(node.label()),newChildren);
    }

    if (nodesToNames.ContainsKey(node))
      newNamesToNodes.put(nodesToNames.get(node),clone);

    return new Tuple<Tree,Tree>(clone,newFoot);
  }



  /***********************************************************/
  /* below here is init stuff for finding the foot node.     */
  /***********************************************************/


  private static readonly String footNodeCharacter = "@";
  private static readonly Regex footNodeLabelPattern = new Regex("^(.*)" + footNodeCharacter + '$', RegexOptions.Compiled);
  private static readonly Regex escapedFootNodeCharacter = new Regex('\\' + footNodeCharacter, RegexOptions.Compiled);

  /**
   * Returns the foot node of the adjunction tree, which is the terminal node
   * that ends in @.  In the process, turns the foot node into a TreeNode
   * (rather than a leaf), and destructively un-escapes all the escaped
   * instances of @ in the tree.  Note that readonly @ in a non-terminal node is
   * ignored, and left in.
   */
  private static Tree findFootNode(Tree t) {
    Tree footNode = findFootNodeHelper(t);
    Tree result = footNode;
    if (footNode != null) {
      Tree newFootNode = footNode.treeFactory().newTreeNode(footNode.label(), new List<Tree>());

      Tree parent = footNode.parent(t);
      if (parent != null) {
        int i = parent.objectIndexOf(footNode);
        parent.setChild(i, newFootNode);
      }

      result = newFootNode;
    }
    return result;
  }

  private static Tree findFootNodeHelper(Tree t) {
    Tree foundDtr = null;
    if (t.isLeaf()) {
      Matcher m = footNodeLabelPattern.matcher(t.label().value());
      if (m.matches()) {
        t.label().setValue(m.group(1));
        return t;
      } else {
        return null;
      }
    }
    foreach (Tree child in t.children()) {
      Tree thisFoundDtr = findFootNodeHelper(child);
      if (thisFoundDtr != null) {
        if (foundDtr != null) {
          throw new TsurgeonParseException("Error -- two foot nodes in subtree" + t.toString());
        } else {
          foundDtr = thisFoundDtr;
        }
      }
    }
    Matcher m = escapedFootNodeCharacter.matcher(t.label().value());
    t.label().setValue(m.replaceAll(footNodeCharacter));
    return foundDtr;
  }


  /***********************************************************
   * below here is init stuff for getting node -> names maps *
   ***********************************************************/

  // There are two ways in which you can can match the start of a name
  // expression.
  // The first is if you have any number of non-escaping characters
  // preceding an "=" and a name.  This is the ([^\\\\]*) part.
  // The second is if you have any number of any characters, followed
  // by a non-"\" character, as "\" is used to escape the "=".  After
  // that, any number of pairs of "\" are allowed, as we let "\" also
  // escape itself.  After that comes "=" and a name.
  static readonly Regex namePattern = new Regex("^((?:[^\\\\]*)|(?:(?:.*[^\\\\])?)(?:\\\\\\\\)*)=([^=]+)$", RegexOptions.Compiled);

  /**
   * Looks for new names, destructively strips them out.
   * Destructively unescapes escaped chars, including "=", as well.
   */
  private void initializeNamesNodesMaps(Tree t) {
    foreach (Tree node in t.subTreeList()) {
      Matcher m = namePattern.matcher(node.label().value());
      if (m.find()) {
        namesToNodes.put(m.group(2), node);
        nodesToNames.put(node, m.group(2));
        node.label().setValue(m.group(1));
      }
      node.label().setValue(unescape(node.label().value()));
    }
  }

  static String unescape(String input) {
    return input.replaceAll("\\\\(.)", "$1");
  }
    }
}