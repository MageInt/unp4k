using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Media;

namespace unp4k.gui.TreeModel
{
	public class DirectoryTreeItem : TreeItem, IBranchItem
	{
		public virtual Boolean Expanded { get; set; } = false;

		public override DateTime LastModifiedUtc => AllChildren
			.OfType<IStreamTreeItem>()
			.Max(t => t.LastModifiedUtc);

		public override Int64 StreamLength => AllChildren
			.OfType<IStreamTreeItem>()
			.Sum(t => t.StreamLength);

		private String _sortKey;
		public override String SortKey =>
			_sortKey = _sortKey ??
			$"{ParentTreeItem?.SortKey}\\__{Text}".Trim('\\');

		private ImageSource _icon;
		public override Object Icon =>
			_icon = _icon ?? 
			IconManager.GetCachedFolderIcon(
				path: RelativePath, 
				iconSize: IconManager.IconSize.Large,
				folderType: IconManager.FolderType.Closed);

		public DirectoryTreeItem(String title)
			: base(title) { }
	}
}
