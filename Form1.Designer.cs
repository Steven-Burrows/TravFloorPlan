namespace TravFloorPlan
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            splitContainer = new SplitContainer();
            paletteListBox = new ListBox();
            canvasPanel = new Panel();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 0);
            splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(paletteListBox);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(canvasPanel);
            splitContainer.Size = new Size(1000, 700);
            splitContainer.SplitterDistance = 200;
            splitContainer.TabIndex = 0;
            // 
            // paletteListBox
            // 
            paletteListBox.Dock = DockStyle.Fill;
            paletteListBox.FormattingEnabled = true;
            paletteListBox.IntegralHeight = false;
            paletteListBox.Location = new Point(0, 0);
            paletteListBox.Name = "paletteListBox";
            paletteListBox.Size = new Size(200, 700);
            paletteListBox.TabIndex = 0;
            // 
            // canvasPanel
            // 
            canvasPanel.BackColor = Color.White;
            canvasPanel.BorderStyle = BorderStyle.FixedSingle;
            canvasPanel.Dock = DockStyle.Fill;
            canvasPanel.Location = new Point(0, 0);
            canvasPanel.Name = "canvasPanel";
            canvasPanel.Size = new Size(796, 700);
            canvasPanel.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 700);
            Controls.Add(splitContainer);
            Name = "Form1";
            Text = "TravFloorPlan";
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ListBox paletteListBox;
        private System.Windows.Forms.Panel canvasPanel;
    }
}
