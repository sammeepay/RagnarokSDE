﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using SDE.ApplicationConfiguration;
using SDE.Core;
using SDE.Core.Avalon;
using SDE.Editor.Engines.Parsers;
using SDE.Editor.Generic;
using SDE.Editor.Generic.Lists;
using SDE.Editor.Generic.Parsers.Generic;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;

namespace SDE.View.Dialogs {
	/// <summary>
	/// Interaction logic for ScriptEditDialog.xaml
	/// </summary>
	public partial class ScriptEditDialog : TkWindow, IInputWindow {
		private readonly CompletionList _li;
		private CompletionWindow _completionWindow;
		private MetaTable<int> _skill_db;
		private List<string> _skills;

		public ScriptEditDialog(string text) : base("Script edit", "cde.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();

			AvalonLoader.Load(_textEditor);
			AvalonLoader.SetSyntax(_textEditor, "Script");

			string script = DbIOFormatting.ScriptFormat(text, 0);
			_textEditor.Text = script;
			_textEditor.TextArea.TextEntered += new TextCompositionEventHandler(_textArea_TextEntered);
			_textEditor.TextArea.TextEntering += new TextCompositionEventHandler(_textArea_TextEntering);
			_textEditor.TextChanged += (e, a) => OnValueChanged();
			//_textEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy();

			_completionWindow = new CompletionWindow(_textEditor.TextArea);
			_completionWindow.Background = Application.Current.Resources["TabItemBackground"] as Brush;
			_li = _completionWindow.CompletionList;
			ListView lv = _li.ListBox;
			lv.SelectionMode = SelectionMode.Single;
			lv.Background = Application.Current.Resources["TabItemBackground"] as Brush;

			//Image
			ListViewDataTemplateHelper.GenerateListViewTemplateNew(lv, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.ImageColumnInfo { Header = "", DisplayExpression = "Image", TextAlignment = TextAlignment.Center, FixedWidth = 22, MaxHeight = 22, SearchGetAccessor = "Commands"},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Commands", DisplayExpression = "Text", TextAlignment = TextAlignment.Left, IsFill = true, ToolTipBinding = "Description"}
			}, null, new string[] { }, "generateHeader", "false");

			_completionWindow.Content = null;
			_completionWindow = null;

			WindowStartupLocation = WindowStartupLocation.CenterOwner;

			_textEditor.Loaded += delegate {
				_textEditor.Focus();
			};
		}

		public string Text {
			get {
				return Methods.Aggregate(_textEditor.Text.Split(new string[] {Environment.NewLine, "\n"}, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim(' ', '\t') + " ").ToList(), "").Trim(' ');
			}
		}

		public Grid Footer { get { return _footerGrid; } }
		public event Action ValueChanged;

		public void OnValueChanged() {
			Action handler = ValueChanged;
			if (handler != null) handler();
		}

		private void _textArea_TextEntering(object sender, TextCompositionEventArgs e) {
			if (e.Text.Length > 0 && _completionWindow != null) {
				if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_') {
					if (e.Text[0] != '\t') {
						// The match must be exact

						string word = AvalonLoader.GetWholeWord(_textEditor.TextArea.Document, _textEditor);

						if (_li.SelectedItem == null || !_li.SelectedItem.ToString().StartsWith(word ?? "", StringComparison.OrdinalIgnoreCase)) {
							_completionWindow.Close();
							return;
						}
					}

					_completionWindow.CompletionList.RequestInsertion(e);
				}
			}
			//if (e.Text.Length > 0 && _completionWindow == null) {
			//	if (e.Text[0] == '}') {
			//		int curLine = _textEditor.TextArea.Caret.Line;
			//		var text = _getText(curLine);
			//		int tabCount = _findIndex(curLine, '}');
			//
			//		if (tabCount > 0) {
			//			if (tabCount == block.OuterIndent.Length + 1) {
			//				// remove one!
			//				doc.Text = block.OuterIndent + line.Substring(tabCount);
			//			}
			//		}
			//	}
			//}
		}

		private string _getText(int line) {
			return _textEditor.Document.GetText(_textEditor.Document.GetLineByNumber(line)).TrimEnd(' ');
		}

		private DocumentLine _getLine(int line) {
			return _textEditor.Document.GetLineByNumber(line);
		}

		private int _findIndex(int line, char end = '*') {
			int tabCount = 0;
			var text = _textEditor.Document.GetText(_textEditor.Document.GetLineByNumber(line)).TrimEnd(' ');

			for (int k = 0; k < text.Length; k++) {
				if (text[k] == '\t')
					tabCount++;
				else if (end != '*' && text[k] == end)
					break;
				else {
					tabCount = -1;
				}
			}

			return tabCount;
		}

		private void _textArea_TextEntered(object sender, TextCompositionEventArgs e) {
			if (e.Text.Length > 0 && (char.IsLetter(e.Text[0]) || e.Text[0] == '_')) {
				_update();
			}
		}

		private void _update() {
			// Open code completion after the user has pressed dot:
			if (_completionWindow == null || !_completionWindow.IsVisible) {
				if (_li.Parent != null) {
					((CompletionWindow)_li.Parent).Content = null;
				}

				_completionWindow = new CompletionWindow(_textEditor.TextArea, _li);
				_completionWindow.Changed += new EventHandler(_completionWindow_Changed);

				_completionWindow.Closed += delegate {
					if (_completionWindow != null) _completionWindow.Content = null;
					_completionWindow = null;
				};
			}

			RangeObservableCollectionX<ICompletionData> data = (RangeObservableCollectionX<ICompletionData>)_li.CompletionData;
			data.Clear();

			if (_skills == null) {
				_skills = new List<string>();
				_skill_db = SdeEditor.Instance.ProjectDatabase.GetMetaTable<int>(ServerDbs.Skills);
				_skill_db.FastItems.ForEach(p => _skills.Add(p.GetStringValue(ServerSkillAttributes.Name.Index)));
			}

			string word = AvalonLoader.GetWholeWord(_textEditor.TextArea.Document, _textEditor);

			List<string> words = ScriptEditorList.Words.Where(p => p.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1).OrderBy(p => p).ToList();
			List<string> constants = ScriptEditorList.Constants.Where(p => p.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1).OrderBy(p => p).ToList();
			List<string> skills = _skills.Where(p => p.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1).OrderBy(p => p).ToList();

			if (words.Count == 0 && constants.Count == 0 && skills.Count == 0) {
				_completionWindow.Close();
				return;
			}

			IEnumerable<ICompletionData> results = words.Select(p => (ICompletionData)new MyCompletionData(p, _textEditor, DataType.Function)).
				Concat(constants.Select(p => (ICompletionData)new MyCompletionData(p, _textEditor, DataType.Constant))).
				Concat(skills.Select(p => (ICompletionData)new MyCompletionData(p, _textEditor, DataType.Skill)));

			data.AddRange(results);

			_completionWindow.CompletionList.ListBox.ItemsSource = data;

			_completionWindow.Show();
			_completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData.FirstOrDefault(p => String.Compare(p.Text, word, StringComparison.OrdinalIgnoreCase) >= 0);
			TokeiLibrary.WPF.Extensions.ScrollToCenterOfView(_completionWindow.CompletionList.ListBox, _completionWindow.CompletionList.SelectedItem);
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape)
				Close();
		}

		private void _completionWindow_Changed(object sender, EventArgs e) {
			_update();
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			if (!SdeAppConfiguration.UseIntegratedDialogsForScripts)
				DialogResult = true;
			Close();
		}

		#region Nested type: MyCompletionData

		public class MyCompletionData : ICompletionData {
			private readonly TextEditor _editor;
			private readonly DataType _type;

			public MyCompletionData(string text, TextEditor editor, DataType type) {
				_editor = editor;
				Text = text;
				Priority = 1;
				_type = type;
			}

			#region ICompletionData Members

			public ImageSource Image {
				get {
					switch (_type) {
						case DataType.Constant:
							return ApplicationManager.PreloadResourceImage("properties.png") as ImageSource;
						case DataType.Function:
							return ApplicationManager.PreloadResourceImage("file_imf.png") as ImageSource;
						case DataType.Skill:
							return ApplicationManager.PreloadResourceImage("sword.png") as ImageSource;
					}
					return null;
				}
			}

			public string Text { get; private set; }

			// Use this property if you want to show a fancy UIElement in the list.
			public object Content {
				get { return Text; }
			}

			public object Description {
				get { return null; }
			}

			public double Priority { get; private set; }

			public void Complete(TextArea textArea, ISegment completionSegment,
			                     EventArgs insertionRequestEventArgs) {
				ISegment seg = AvalonLoader.GetWholeWordSegment(textArea.Document, _editor);
				textArea.Document.Replace(seg, Text);
			}

			#endregion
		}

		#endregion

		public void DisableOk() {
			_buttonOk.Visibility = Visibility.Hidden;
		}
	}

	public enum DataType {
		Function,
		Constant,
		Skill
	}
}
