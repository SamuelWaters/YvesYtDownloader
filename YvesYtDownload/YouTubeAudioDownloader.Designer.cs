// MainForm.Designer.cs
namespace YvesYtDownload
{
    partial class YoutubeDownloader
    {
        private System.ComponentModel.IContainer components = null;
        private TextBox urlTextBox;
        private Button downloadButton;
        private Label statusLabel;
        private ProgressBar progressBar1;
        private Button browseButton;
        private FolderBrowserDialog folderBrowserDialog;
        private TextBox outputDirectoryTextBox;

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.components != null)
            {
                this.components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.urlTextBox = new TextBox();
            this.downloadButton = new Button();
            this.statusLabel = new Label();
            this.progressBar1 = new ProgressBar();
            this.browseButton = new Button();
            this.folderBrowserDialog = new FolderBrowserDialog();
            this.outputDirectoryTextBox = new TextBox();
            this.openExplButton = new Button();
            this.SuspendLayout();
            // 
            // urlTextBox
            // 
            this.urlTextBox.Location = new Point(18, 20);
            this.urlTextBox.Margin = new Padding(4);
            this.urlTextBox.Name = "urlTextBox";
            this.urlTextBox.PlaceholderText = "https://www.youtube.com/watch?v=...";
            this.urlTextBox.Size = new Size(534, 27);
            this.urlTextBox.TabIndex = 0;
            // 
            // downloadButton
            // 
            this.downloadButton.Location = new Point(569, 16);
            this.downloadButton.Margin = new Padding(4);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new Size(107, 35);
            this.downloadButton.TabIndex = 1;
            this.downloadButton.Text = "Download";
            this.downloadButton.UseVisualStyleBackColor = true;
            this.downloadButton.Click += this.downloadButton_Click;
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new Point(18, 150);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new Size(236, 20);
            this.statusLabel.TabIndex = 2;
            this.statusLabel.Text = "Enter a valid YouTube URL to start.";
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new Point(18, 116);
            this.progressBar1.Margin = new Padding(4);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new Size(658, 30);
            this.progressBar1.TabIndex = 3;
            this.progressBar1.Click += this.progressBar1_Click;
            // 
            // browseButton
            // 
            this.browseButton.Location = new Point(569, 66);
            this.browseButton.Margin = new Padding(4);
            this.browseButton.Name = "browseButton";
            this.browseButton.Size = new Size(107, 35);
            this.browseButton.TabIndex = 4;
            this.browseButton.Text = "Select...";
            this.browseButton.UseVisualStyleBackColor = true;
            this.browseButton.Click += this.browseButton_Click;
            // 
            // outputDirectoryTextBox
            // 
            this.outputDirectoryTextBox.Location = new Point(18, 70);
            this.outputDirectoryTextBox.Margin = new Padding(4);
            this.outputDirectoryTextBox.Name = "outputDirectoryTextBox";
            this.outputDirectoryTextBox.PlaceholderText = "Select Output Directory";
            this.outputDirectoryTextBox.ReadOnly = true;
            this.outputDirectoryTextBox.Size = new Size(534, 27);
            this.outputDirectoryTextBox.TabIndex = 5;
            // 
            // openExplButton
            // 
            this.openExplButton.Location = new Point(569, 163);
            this.openExplButton.Name = "openExplButton";
            this.openExplButton.Size = new Size(107, 35);
            this.openExplButton.TabIndex = 6;
            this.openExplButton.Text = "Browse 😈";
            this.openExplButton.UseVisualStyleBackColor = true;
            this.openExplButton.Click += this.openExpl_Click;
            // 
            // YoutubeDownloader
            // 
            this.AutoScaleDimensions = new SizeF(8F, 20F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(688, 211);
            this.Controls.Add(this.openExplButton);
            this.Controls.Add(this.outputDirectoryTextBox);
            this.Controls.Add(this.browseButton);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.downloadButton);
            this.Controls.Add(this.urlTextBox);
            this.Margin = new Padding(4);
            this.Name = "YoutubeDownloader";
            this.Text = "Yves' YouTube Audio Downloader";
            this.Load += this.MainForm_Load;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private Button openExplButton;
    }
}
