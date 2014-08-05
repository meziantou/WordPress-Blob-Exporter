using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace WordPressBlobExporter
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void ButtonSrcPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "XML file (*.xml)|*.xml";
            dialog.CheckFileExists = true;
            var dialogResult = dialog.ShowDialog();
            if (dialogResult == DialogResult.OK || dialogResult == DialogResult.Yes)
            {
                textBoxSourcePath.Text = dialog.FileName;
            }
        }

        private void ButtonDstPath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            var dialogResult = dialog.ShowDialog();
            if (dialogResult == DialogResult.OK || dialogResult == DialogResult.Yes)
            {
                textBoxDestinationPath.Text = dialog.SelectedPath;
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            buttonExport.Enabled = !string.IsNullOrWhiteSpace(textBoxSourcePath.Text) && !string.IsNullOrWhiteSpace(textBoxDestinationPath.Text);
        }

        private async void buttonExport_Click(object sender, EventArgs e)
        {
            try
            {
                buttonExport.Enabled = false;
                progressBar1.Visible = true;
                progressBar1.Value = 0;

                var dstPath = textBoxDestinationPath.Text;
                if (!Directory.Exists(dstPath))
                {
                    Directory.CreateDirectory(dstPath);
                }

                ConcurrentBag<string> errors = new ConcurrentBag<string>();

                var document = new XmlDocument();
                document.Load(textBoxSourcePath.Text);
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(document.NameTable);
                nsmgr.AddNamespace("wp", "http://wordpress.org/export/1.2/");
                var nodes = document.SelectNodes("//item[wp:attachment_url]", nsmgr);
                if (nodes != null)
                {
                    progressBar1.Maximum = nodes.Count;

                    await Task.Run(() =>
                    {
                        Parallel.ForEach(nodes.OfType<XmlElement>(), node =>
                        {
                            var nodeId = node.SelectSingleNode("wp:post_id", nsmgr);
                            if (nodeId == null)
                                return;

                            var nodeUrl = node.SelectSingleNode("wp:attachment_url", nsmgr);
                            if (nodeUrl == null)
                                return;

                            try
                            {
                                var id = nodeId.InnerText;
                                string extension = null;
                                try
                                {
                                    extension = Path.GetExtension(nodeUrl.InnerText);
                                }
                                catch
                                { }
                                var path = Path.Combine(dstPath, id + extension);
                                using (WebClient client = new WebClient())
                                {
                                    client.DownloadFile(nodeUrl.InnerText, path);
                                }
                            }
                            catch
                            {
                                errors.Add(nodeUrl.InnerText);
                            }

                            progressBar1.Invoke(new Action(() => progressBar1.Increment(1)));
                        });
                    });

                    if (errors.Count > 0)
                    {
                        MessageBox.Show("Errors:" + Environment.NewLine + string.Join(Environment.NewLine, errors), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                buttonExport.Enabled = true;
                progressBar1.Visible = false;
            }
        }
    }
}
