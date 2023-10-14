using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Media;
using ICSharpCode.TreeView;
using System.IO;
using unp4k.gui.Plugins;

namespace unp4k.gui.TreeModel
{
	public interface ITreeItem
	{
		String Title { get; }
		String SortKey { get; }
		String RelativePath { get; }

		DateTime LastModifiedUtc { get; }
		Int64 StreamLength { get; }

		ITreeItem ParentTreeItem { get; }
		IEnumerable<ITreeItem> AllChildren { get; }
		
		SharpTreeNodeCollection Children { get; }
		SharpTreeNode Parent { get; }
		Object Text { get; }
		Object Icon { get; }
		Object ToolTip { get; }
		Int32 Level { get; }
		Boolean IsRoot { get; }
		Boolean IsHidden { get; set; }
		Boolean IsVisible { get; }
		Boolean IsSelected { get; set; }
	}

	public interface IBranchItem : ITreeItem { }

	public abstract class TreeItem : SharpTreeNode, ITreeItem
	{
		public virtual String Title { get; }
		public ITreeItem ParentTreeItem => Parent as ITreeItem;
		
		private String _sortKey;
		public virtual String SortKey => 
			_sortKey = _sortKey ??
			$"{ParentTreeItem?.SortKey}\\{Text}".Trim('\\');

		private String _relativePath;
		public virtual String RelativePath =>
			_relativePath = _relativePath ?? 
			$"{ParentTreeItem?.RelativePath}\\{Text}".Trim('\\');

		private IEnumerable<ITreeItem> _allChildren;
		public virtual IEnumerable<ITreeItem> AllChildren =>
			_allChildren = _allChildren ??
			Children
				.OfType<ITreeItem>()
				.SelectMany(c => c.AllChildren.Union(new[] { c }))
				.OfType<ITreeItem>()
				.ToArray();

		public override Object Text => Title;

		public abstract DateTime LastModifiedUtc { get; }
		public abstract Int64 StreamLength { get; }

		private char[] CharSeparator = new[] { '/', '\\' };

		internal TreeItem(String title)
		{
			Title = title;
		}

		// TODO: Factory selection
		//private IFormatFactory[] factories = new IFormatFactory[] {
		//	new DataForgeFormatFactory { },
		//	new CryXmlFormatFactory { }
		//};

		public ITreeItem AddStream(string fullPath, Func<Stream> @delegate, DateTime lastModifiedUtc, long streamLength)
		{
			string[] ParentSplitPath = Path.GetDirectoryName(fullPath).Split(CharSeparator, StringSplitOptions.RemoveEmptyEntries);

			ITreeItem parent = GetParentRelativePath(ParentSplitPath); //24%CPU

			if (parent == null) return null;

			StreamTreeItem streamItem = new StreamTreeItem(Path.GetFileName(fullPath), @delegate, lastModifiedUtc, streamLength);

			parent.Children.Add(streamItem);

			return streamItem;
		}

		internal ITreeItem GetParentRelativePath(string[] fullPath)
		{
			if (fullPath.Length == 0) return this;

			string key = fullPath[0];

			DirectoryTreeItem directory = Children.OfType<DirectoryTreeItem>().FirstOrDefault(d => d.Title == key);

			if (directory == null)
			{
				directory = new DirectoryTreeItem(key);

				Children.Add(directory);
			}

			return directory.GetParentRelativePath(fullPath.Skip(1).ToArray());
		}

		public void Sort()
		{
			if (Children.Count > 1)
			{
				Children.Sort((x1, x2) =>
				{
					if (x1 is TreeItem t1)
					{
						if (x2 is TreeItem t2)
						{
							return String.Compare(t1.SortKey, t2.SortKey, StringComparison.InvariantCultureIgnoreCase);
						}
					}

					return String.Compare($"{x1.Text}", $"{x2.Text}", StringComparison.InvariantCultureIgnoreCase);
				});
			}

			foreach (var child in Children.OfType<TreeItem>())
			{
				child.Sort();
			}
		}
	}
}
