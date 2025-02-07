﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using unp4k.gui.Plugins;
using unp4k.gui.Extensions;
using System.Windows.Media;
using System.Windows.Controls;
using System.Diagnostics;

namespace unp4k.gui.TreeModel
{
	public class DataForgeTreeItem : StreamTreeItem, IStreamTreeItem, IBranchItem, ITreeItem
	{
		public override String RelativePath => ParentTreeItem.RelativePath;
		public virtual Boolean Expanded { get; set; }
		
		public DataForgeTreeItem(IStreamTreeItem node, unforge.DataForge dataForge)
			: base(node.Title, () => dataForge.GetStream(), node.LastModifiedUtc, dataForge.OuterXML.Length)
		{
			var sw = new Stopwatch { };

			sw.Start();

			var maxIndex = dataForge.Length - 1;
			var lastIndex = 0L;

			var oldProgress = ArchiveExplorer.RegisterProgress(async (ProgressBar barProgress) =>
			{
				barProgress.Maximum = maxIndex;
				barProgress.Value = lastIndex;

				await ArchiveExplorer.UpdateStatus($"Deserializing file {lastIndex:#,##0}/{maxIndex:#,##0} from dataforge");

				await Task.CompletedTask;
			});

			foreach ((String FileName, XmlDocument XmlDocument) entry in dataForge)
			{
				AddStream(
					entry.FileName,
					() => entry.XmlDocument.GetStream(),
					node.LastModifiedUtc,
					entry.XmlDocument.OuterXml.Length);
				
				lastIndex++;
			}

			sw.Stop();

			ArchiveExplorer.RegisterProgress(oldProgress);

			ArchiveExplorer.UpdateStatus($"Deserialized {Text} in {sw.ElapsedMilliseconds:#,000}ms").Wait();
		}
	}

	public class CryXmlTreeItem : StreamTreeItem, IStreamTreeItem, ITreeItem
	{
		public CryXmlTreeItem(IStreamTreeItem node, XmlDocument xml)
			: base(node.Title, () => xml.GetStream(), node.LastModifiedUtc, xml.OuterXml.Length)
		{ }
	}
}
