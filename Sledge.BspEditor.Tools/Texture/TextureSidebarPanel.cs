using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogicAndTrick.Oy;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Primitives.MapData;
using Sledge.Common.Shell.Commands;
using Sledge.Common.Shell.Components;
using Sledge.Common.Shell.Context;
using Sledge.Common.Shell.Documents;
using Sledge.Common.Shell.Hooks;
using Sledge.Common.Translations;
using Sledge.Providers.Texture;
using Sledge.Shell;
using System.Linq;
using System.Reactive.Linq;

using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Modification.Operations.Data;
using Sledge.BspEditor.Primitives;
using Sledge.BspEditor.Primitives.MapObjectData;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;
using Sledge.Shell.Forms;

namespace Sledge.BspEditor.Tools.Texture
{
    [AutoTranslate]
    [Export(typeof(ISidebarComponent))]
    [Export(typeof(IInitialiseHook))]
    [OrderHint("B")]
    public partial class TextureSidebarPanel : UserControl, ISidebarComponent, IInitialiseHook
    {
        public Task OnInitialise()
        {
            Oy.Subscribe<IDocument>("Document:Activated", DocumentActivated);
            Oy.Subscribe<Change>("MapDocument:Changed", DocumentChanged);
            return Task.FromResult(0);
        }

        public string Title { get; set; } = "Texture";
        public object Control => this;

        private string _currentTexture;
        private WeakReference<MapDocument> _activeDocument;

        private readonly List<string> _recentTextures = new List<string>();
        private TextureListPanel RecentTexturesList;

        public string Apply
        {
            set => this.InvokeLater(() => ApplyButton.Text = value);
        }

        public string Browse
        {
            set => this.InvokeLater(() => BrowseButton.Text = value);
        }

        public string Replace
        {
            set => this.InvokeLater(() => ReplaceButton.Text = value);
        }

        public TextureSidebarPanel()
        {
            CreateHandle();
            InitializeComponent();
            InitialiseTextureList();

            SizeLabel.Text = "";
            NameLabel.Text = "";
            _activeDocument = new WeakReference<MapDocument>(null);

            RecentTexturesList.HighlightedTexturesChanged += TextureListHighlightedTexturesChanged;

        }

        private void ApplyButtonClicked(object sender, EventArgs e)
        {
            Oy.Publish("Command:Run", new CommandMessage("BspEditor:ApplyActiveTexture"));
        }

        private void BrowseButtonClicked(object sender, EventArgs e)
        {
            Oy.Publish("Command:Run", new CommandMessage("BspEditor:BrowseActiveTexture"));
        }

        private void ReplaceButtonClicked(object sender, EventArgs e)
        {
            Oy.Publish("Command:Run", new CommandMessage("BspEditor:ReplaceTextures"));
        }

        private async Task DocumentActivated(IDocument doc)
        {
            var md = doc as MapDocument;

            _activeDocument = new WeakReference<MapDocument>(md);
            _currentTexture = null;

            if (md != null)
            {
                Environment.TextureCollection tc = await md.Environment.GetTextureCollection();
                RecentTexturesList.Collection = tc;
            }
            else
                RecentTexturesList.Collection = null;

            await this.InvokeAsync(() =>
            {
                var dis = SelectionPictureBox.Image;
                SelectionPictureBox.Image = null;
                dis?.Dispose();
            });

            if (md != null)
            {
                await TextureSelected(md.Map.Data.GetOne<ActiveTexture>()?.Name);
            }
        }

        private async Task DocumentChanged(Change change)
        {
            if (_activeDocument.TryGetTarget(out MapDocument t) && change.Document == t)
            {
                if (change.HasDataChanges && change.AffectedData.Any(x => x is ActiveTexture))
                    await ActiveTextureChanged(t.Map.Data.GetOne<ActiveTexture>()?.Name);
            }
        }

        public bool IsInContext(IContext context)
        {
            return context.TryGet("ActiveDocument", out MapDocument _);
        }

        private void InitialiseTextureList()
        {
            RecentTexturesList = new TextureListPanel
            {
                AllowMultipleHighlighting = false,
                AllowHighlighting = true,
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Black,
                EnableDrag = false,
                // 64 in the texture application dialog, but we have more space here
                ImageSize = 128
            };

            RecentTextureListPanel.Controls.Add(RecentTexturesList);
        }

        private async void TextureListHighlightedTexturesChanged(object sender, IEnumerable<string> sel)
        {
            List<string> selection = sel.ToList();
            string item = selection.FirstOrDefault();

            if (selection.Any())
                RecentTexturesList.SetHighlightedTextures(new string[0]);
            else
                item = RecentTexturesList.GetHighlightedTextures().FirstOrDefault();

            // Actually change it
        }

        private void UpdateRecentTextureList()
        {
            RecentTexturesList.SetTextureList(_recentTextures);
        }

        private async Task ActiveTextureChanged(string item)
        {
            if (string.IsNullOrWhiteSpace(item))
                return;
           
            // It's terrible to maintain separate lists here and in the texture 
            // application dialog, but they should be synchronized properly
            _recentTextures.Remove(item);
            _recentTextures.Insert(0, item);

            if (_recentTextures.Count > 10) 
                _recentTextures.RemoveRange(10, _recentTextures.Count - 10);
            UpdateRecentTextureList();

            if (RecentTexturesList.GetTextureList().Contains(item))
            {
                RecentTexturesList.SetHighlightedTextures(new[] {item});
                RecentTexturesList.ScrollToTexture(item);
            }

            await TextureSelected(item);
        }



        private async Task TextureSelected(string selection)
        {
            if (selection == _currentTexture) return;
            _currentTexture = selection;

            if (!_activeDocument.TryGetTarget(out MapDocument doc)) return;

            Bitmap bmp = null;
            TextureItem texItem = null;

            if (selection != null)
            {
                var tc = await doc.Environment.GetTextureCollection();
                texItem = await tc.GetTextureItem(selection);

                if (texItem != null)
                {
                    using (var ss = tc.GetStreamSource())
                    {
                        bmp = await ss.GetImage(selection, 256, 256);
                    }
                }
            }

            this.InvokeLater(() =>
            {
                if (bmp != null)
                {
                    if (bmp.Width > SelectionPictureBox.Width || bmp.Height > SelectionPictureBox.Height)
                    {
                        SelectionPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    else
                    {
                        SelectionPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
                    }
                }

                var dis = SelectionPictureBox.Image;
                SelectionPictureBox.Image = null;
                dis?.Dispose();

                SelectionPictureBox.Image = bmp;
                NameLabel.Text = texItem?.Name ?? "";
                SizeLabel.Text = texItem == null ? "" : $"{texItem.Width} x {texItem.Height}";
            });
        }
    }
}
