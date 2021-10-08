﻿using System.Windows;
using System.Windows.Controls;
using SDE.ApplicationConfiguration;
using TokeiLibrary;

namespace SDE.Editor.Generic.UI.CustomControls {
	/// <summary>
	/// Interaction logic for PreviewItemInGame.xaml
	/// </summary>
	public partial class PreviewItemInGame : UserControl {
		public PreviewItemInGame(TextBox nameBox) {
			Box = nameBox;
			InitializeComponent();
			Margin = new Thickness(3);

			if (SdeAppConfiguration.ThemeIndex == 1) {
				_resImage.Source = ApplicationManager.GetResourceImage("collection_bg_dark.png");
			}
		}

		public Image PreviewImage {
			get { return _itemImage; }
		}

		public RichTextBox PreviewDescription {
			get { return _rtbItemDescription; }
		}

		public TextBox Box { get; set; }
	}
}