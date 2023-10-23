// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Collections.Specialized;
using System.Collections;

namespace ICSharpCode.TreeView
{
	public sealed class SharpTreeNodeCollection : IList<SharpTreeNode>, ICollection<SharpTreeNode>, IEnumerable<SharpTreeNode>, IEnumerable, INotifyCollectionChanged
	{
		private readonly SharpTreeNode parent;

		private List<SharpTreeNode> list = new List<SharpTreeNode>();

		private bool isRaisingEvent;

		public SharpTreeNode this[int index]
		{
			get
			{
				return list[index];
			}
			set
			{
				ThrowOnReentrancy();
				SharpTreeNode sharpTreeNode = list[index];
				if (sharpTreeNode != value)
				{
					ThrowIfValueIsNullOrHasParent(value);
					list[index] = value;
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, sharpTreeNode, index));
				}
			}
		}

		public int Count => list.Count;

		bool ICollection<SharpTreeNode>.IsReadOnly => false;

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public SharpTreeNodeCollection(SharpTreeNode parent)
		{
			this.parent = parent;
		}

		private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			Debug.Assert(!isRaisingEvent);
			isRaisingEvent = true;
			try
			{
				parent.OnChildrenChanged(e);
				if (this.CollectionChanged != null)
				{
					this.CollectionChanged(this, e);
				}
			}
			finally
			{
				isRaisingEvent = false;
			}
		}

		private void ThrowOnReentrancy()
		{
			if (isRaisingEvent)
			{
				throw new InvalidOperationException();
			}
		}

		private void ThrowIfValueIsNullOrHasParent(SharpTreeNode node)
		{
			if (node == null)
			{
				throw new ArgumentNullException("node");
			}

			if (node.modelParent != null)
			{
				throw new ArgumentException("The node already has a parent", "node");
			}
		}

		public int IndexOf(SharpTreeNode node)
		{
			if (node == null || node.modelParent != parent)
			{
				return -1;
			}

			return list.IndexOf(node);
		}

		public void Insert(int index, SharpTreeNode node)
		{
			ThrowOnReentrancy();
			ThrowIfValueIsNullOrHasParent(node);
			list.Insert(index, node);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, node, index));
		}

		public void InsertRange(int index, IEnumerable<SharpTreeNode> nodes)
		{
			if (nodes == null)
			{
				throw new ArgumentNullException("nodes");
			}

			ThrowOnReentrancy();
			List<SharpTreeNode> list = nodes.ToList();
			if (list.Count == 0)
			{
				return;
			}

			foreach (SharpTreeNode item in list)
			{
				ThrowIfValueIsNullOrHasParent(item);
			}

			this.list.InsertRange(index, list);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, index));
		}

		public void RemoveAt(int index)
		{
			ThrowOnReentrancy();
			SharpTreeNode changedItem = list[index];
			list.RemoveAt(index);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, changedItem, index));
		}

		public void RemoveRange(int index, int count)
		{
			ThrowOnReentrancy();
			if (count != 0)
			{
				List<SharpTreeNode> range = list.GetRange(index, count);
				list.RemoveRange(index, count);
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, range, index));
			}
		}

		public void Add(SharpTreeNode node)
		{
			ThrowOnReentrancy();
			ThrowIfValueIsNullOrHasParent(node);
			list.Add(node);
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, node, list.Count - 1));
		}

		public void AddRange(IEnumerable<SharpTreeNode> nodes)
		{
			InsertRange(Count, nodes);
		}

		public void Sort(Comparison<SharpTreeNode> comparison)
		{
			list.Sort(comparison);
		}

		public void Sort(IComparer<SharpTreeNode> comparer)
		{
			list.Sort(comparer);
		}

		public void Clear()
		{
			ThrowOnReentrancy();
			List<SharpTreeNode> changedItems = list;
			list = new List<SharpTreeNode>();
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, changedItems, 0));
		}

		public bool Contains(SharpTreeNode node)
		{
			return IndexOf(node) >= 0;
		}

		public void CopyTo(SharpTreeNode[] array, int arrayIndex)
		{
			list.CopyTo(array, arrayIndex);
		}

		public bool Remove(SharpTreeNode item)
		{
			int num = IndexOf(item);
			if (num >= 0)
			{
				RemoveAt(num);
				return true;
			}

			return false;
		}

		public IEnumerator<SharpTreeNode> GetEnumerator()
		{
			return list.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return list.GetEnumerator();
		}

		public void RemoveAll(Predicate<SharpTreeNode> match)
		{
			if (match == null)
			{
				throw new ArgumentNullException("match");
			}

			ThrowOnReentrancy();
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				isRaisingEvent = true;
				bool flag;
				try
				{
					flag = match(list[i]);
				}
				finally
				{
					isRaisingEvent = false;
				}

				if (!flag)
				{
					if (num < i)
					{
						RemoveRange(num, i - num);
						i = num - 1;
					}
					else
					{
						num = i + 1;
					}

					Debug.Assert(num == i + 1);
				}
			}

			if (num < list.Count)
			{
				RemoveRange(num, list.Count - num);
			}
		}
	}
}
