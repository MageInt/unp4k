﻿using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.TreeView;
using Ookii.Dialogs.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using unp4k.gui.Extensions;
using unp4k.gui.TreeModel;
using unp4k.gui.Views;
using Path = System.IO.Path;

namespace unp4k.gui
{
	//public class OpenFileCommand : ICommand
	//{
	//	public void Execute(Object parameter)
	//	{
	//		MessageBox.Show(@"""Hello, world!"" from "
	//			+ (parameter ?? "somewhere secret").ToString());
	//	}

	//	public Boolean CanExecute(Object parameter)
	//	{
	//		return true;
	//	}

	//	public event EventHandler CanExecuteChanged;
	//}

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class ArchiveExplorer : Window
	{
		private Stream _pakFile;
		private ZipFile _pak;
		private TreeExtractor _extractor;
		private ZipFileTreeItem _root;

		private static ArchiveExplorer _instance;

		public const Int32 FILTER_DELAY = 250;
		public const Int32 FILTER_PING = 50;

		public ArchiveExplorer()
		{
			ArchiveExplorer._instance = this;

			InitializeComponent();

			Icon = IconManager.GetCachedFileIcon("data.zip", IconManager.IconSize.Large);

			trvFileExploder.Focus();

			new Thread(async () =>
			{
				while (true)
				{
					await Task.Delay(FILTER_PING);

					while (_lastFilterText != _activeFilterText)
					{
						var now = _lastFilterTime ?? DateTime.Now;

						while ((DateTime.Now - now).TotalMilliseconds < FILTER_DELAY)
						{
							await Task.Delay(FILTER_PING);
							now = _lastFilterTime ?? DateTime.Now;
						}

						if (_root != null)
						{
							var filterText = _lastFilterText;

							var sw = new Stopwatch();

							sw.Start();

							var allChildren = _root.AllChildren.ToArray();

							await Dispatcher.Invoke(async () =>
							{
								foreach (var child in allChildren)
								{
									child.IsHidden = !Filter(child);
								}
							});

							// await this.NotifyNodesAsync(this._root);

							sw.Stop();

							await ArchiveExplorer.UpdateStatus($"Filter took {sw.ElapsedMilliseconds:#,##0}ms");

							_activeFilterText = filterText;
						}

						await Task.Delay(FILTER_PING);
					}
				}
			}).Start();

			new Thread(async () =>
			{
				while (true)
				{
					await Dispatcher.Invoke(async () =>
					{
						await ArchiveExplorer._updateDelegate(barProgress);
					});

					await Task.Delay(100);
				}
			}).Start();
		}

		~ArchiveExplorer()
		{
			if (_pak != null)
			{
				_pak.Close();
				_pak = null;
			}

			if (_pakFile != null)
			{
				_pakFile.Dispose();
				_pakFile = null;
			}
		}

		private static Func<ProgressBar, Task> _updateDelegate = async (ProgressBar barProgress) =>
		{
			await Task.CompletedTask;
		};

		public static Func<ProgressBar, Task> RegisterProgress(Func<ProgressBar, Task> @delegate)
		{
			var oldDelegate = ArchiveExplorer._updateDelegate;

			ArchiveExplorer._updateDelegate = @delegate;
			
			return oldDelegate;
		}

		public static async Task UpdateStatus(String message)
		{
			await ArchiveExplorer._instance.Dispatcher.Invoke(async () =>
			{
				ArchiveExplorer._instance.lblProgress.Text = message;

				await Task.CompletedTask;
			});
		}

		public async Task OpenP4kAsync(string path)
		{
			var treeView = trvFileExploder;
			FileStream pakFile = File.OpenRead(path);
			ZipFile pak = new ZipFile(pakFile) { Key = new Byte[] { 0x5E, 0x7A, 0x20, 0x02, 0x30, 0x2E, 0xEB, 0x1A, 0x3B, 0xB6, 0x17, 0xC3, 0x0F, 0xDE, 0x1E, 0x47 } };

			ZipFileTreeItem root = new ZipFileTreeItem(pak, Path.GetFileName(path));
			
			var filter = _lastFilterText;
			
			if (filter.Equals("Filter...", StringComparison.InvariantCultureIgnoreCase)) filter = null;

			await Dispatcher.Invoke(async () =>
			{
				if (_pak != null)
				{
					_pak.Close();
					_pak = null;
				}

				if (_pakFile != null)
				{
					_pakFile.Dispose();
					_pakFile = null;
				}

				_pak = pak;
				_pakFile = pakFile;

				_extractor = new TreeExtractor(pak, Filter);
				_root = root;

				treeView.Root = root;

				treeView.ClearSort();
				treeView.AddSort(new SortDescription(nameof(ITreeItem.SortKey), ListSortDirection.Ascending));

				await Task.CompletedTask;
			});
		}

		public Predicate<Object> Filter => (Object n) =>
		{
			var filter = _lastFilterText;

			if (String.IsNullOrWhiteSpace(filter)) return true;

			if (n is IBranchItem branch)
			{
				var result = branch.AllChildren.Any(z => z.RelativePath.Contains(filter, CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols));
				return result;
			}

			if (n is IStreamTreeItem leaf)
			{
				var result = leaf.RelativePath.Contains(filter, CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols);
				return result;
			}

			return false;
		};

		private void trvFileExplorer_Expanded(object sender, RoutedEventArgs e)
		{
			var node = (e.OriginalSource as TreeViewItem)?.DataContext as TreeItem;

			if (node != null)
			{
				node.Children.Sort((SharpTreeNode x1, SharpTreeNode x2) => String.Compare($"{x1.Text}", $"{x2.Text}", true));
			}
		}

		private void trvFileExplorer_Collapsed(object sender, RoutedEventArgs e) { }

		private async void cmdOpenArchive_Executed(Object sender, ExecutedRoutedEventArgs e)
		{
			var openFileDialog = new VistaOpenFileDialog
			{
				Filter = "Star Citizen Data Files|*.p4k",
				CheckFileExists = true,
				AddExtension = true,
				DefaultExt = ".p4k"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				// Move to background thread
				new Thread(async () => await OpenP4kAsync(openFileDialog.FileName)).Start();
			}

			await Task.CompletedTask;
		}

		private async void cmdExtractFile_Executed(Object sender, ExecutedRoutedEventArgs e)
		{
			var selectedItems = trvFileExploder.SelectedItems
				.OfType<ITreeItem>()
				.ToArray();

			// Move to background thread
			new Thread(async () =>
			{
				foreach (ITreeItem selectedItem in selectedItems)
				{
					var result = await _extractor.ExtractNodeAsync(selectedItem, false);
					
					// TODO: Handle false(error) results
				}
			}).Start();

			await Task.CompletedTask;
		}

		private async void cmdOpenFile_Executed(Object sender, ExecutedRoutedEventArgs e)
		{
			var selectedItems = trvFileExploder.SelectedItems
				.OfType<ITreeItem>()
				.ToArray();

			// Move to background thread
			new Thread(async () =>
			{
				var result = true;
				
				foreach (ITreeItem selectedItem in selectedItems)
				{
					result &= await _extractor.ExtractNodeAsync(selectedItem, true);
				}
				
				// TODO: Handle false(error) results
			}).Start();

			await Task.CompletedTask;
		}

		#region Mouse Support

		private async void trvFileExploder_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var selectedNode = e.Source as SharpTreeNodeView;

			if (selectedNode == null)
			{
				return;
			}

			ITreeItem selectedItem = selectedNode.DataContext as ITreeItem;

			if (selectedItem == null || selectedItem.Children.Any()) return;

			await _extractor.ExtractNodeAsync(selectedItem, true);
		}

		#endregion

		#region Keyboard Support

		// private Dictionary<Key, Boolean> keyState = new Dictionary<Key, Boolean> { { Key.Enter, false } };
		// 
		// private async void trvFileExplorer_KeyDown(object sender, KeyEventArgs e)
		// {
		// 	this.keyState[e.Key] = true;
		// 
		// 	await Task.CompletedTask;
		// }
		// 
		// private async void trvFileExplorer_KeyUp(object sender, KeyEventArgs e)
		// {
		// 	if (this.keyState[Key.Enter])
		// 	{
		// 		var selectedItem = trvFileExplorer.SelectedItem as TreeModel.TreeItem;
		// 
		// 		if (selectedItem != null)
		// 		{
		// 			var useTemp = trvFileExplorer.SelectedItem is ZipEntryTreeItem;
		// 
		// 			new Thread(async () => await this._extractor.ExtractNodeAsync(selectedItem, useTemp)).Start();
		// 		}
		// 	}
		// 
		// 	this.keyState[e.Key] = false;
		// 
		// 	await Task.CompletedTask;
		// }

		#endregion
			
		#region Filter Support
		
		private DateTime? _lastFilterTime;
		private String _lastFilterText = String.Empty;
		private String _activeFilterText = String.Empty;
		
		private async Task NotifyNodesAsync(ITreeItem node)
		{
			await Task.CompletedTask;
		}

		private async void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
		{
			var filter = txtFilter.Text;
			if (filter.Equals("Filter...", StringComparison.InvariantCultureIgnoreCase)) filter = String.Empty;

			if (filter == _lastFilterText) return;

			_lastFilterTime = DateTime.Now;
			_lastFilterText = filter;

			await Task.CompletedTask;
		}

		#endregion

		#region Placeholder Text Support

		private void txtFilter_GotFocus(object sender, RoutedEventArgs e)
		{
			if (txtFilter.Text.Equals("Filter...", StringComparison.InvariantCultureIgnoreCase))
			{
				txtFilter.Text = String.Empty;
			}
		}

		private void txtFilter_LostFocus(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrWhiteSpace(txtFilter.Text))
			{
				txtFilter.Text = "Filter...";
			}
		}

		#endregion

		//#region Outbound Drag and Drop Support

		//private Point _start;

		//private void trvFileExplorer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		//{
		//	this._start = e.GetPosition(null);
		//}

		//private void trvFileExplorer_MouseMove(object sender, MouseEventArgs e)
		//{
		//	if (e.LeftButton != MouseButtonState.Pressed) return;
		//	if (this.trvFileExplorer.SelectedItem == null) return;

		//	Point mpos = e.GetPosition(null);
		//	Vector diff = this._start - mpos;

		//	if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance &&
		//		Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
		//	{
		//		// right about here you get the file urls of the selected items.
		//		// should be quite easy, if not, ask.
		//		String[] files = new String[] { };
		//		String dataFormat = DataFormats.FileDrop;
		//		DataObject dataObject = new DataObject(dataFormat, files);
		//		DragDrop.DoDragDrop(this.trvFileExplorer, dataObject, DragDropEffects.Move);
		//	}
		//}

		//#endregion
		
		private void cmdExitApplication_Executed(Object sender, ExecutedRoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void cmdOpenAbout_Executed(Object sender, ExecutedRoutedEventArgs e)
		{
			About AboutWindow = new About();
			AboutWindow.ShowDialog();
		}

		private void cmdFilterArchive_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			txtFilter.Focus();
			txtFilter.SelectAll();
		}
	}
}
