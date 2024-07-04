using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DirectoryTree
{
    public partial class MainForm : Form
    {
        private CancellationTokenSource _cancellationToken;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            txtSearch.Text = Properties.Settings.Default.LastSearch;
            if (!String.IsNullOrEmpty(Properties.Settings.Default.LastSelectedPath))
                BuildTreeView(Properties.Settings.Default.LastSelectedPath, "*.*");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.LastSearch = txtSearch.Text;
            Properties.Settings.Default.Save();
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            string strSearch = txtSearch.Text;
            if (string.IsNullOrEmpty(strSearch))
            {
                MessageBox.Show("Нечего искать");
                return;
            }

            lblTime.Text = string.Empty;
            lblFoundCount.Text = string.Empty;
            stopwatch.Reset();

            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.SelectedPath = Properties.Settings.Default.LastSelectedPath;
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                stopwatch.Start();
                timer.Start();

                string Path = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.LastSelectedPath = Path;

                _cancellationToken = new CancellationTokenSource();
                var Token = _cancellationToken.Token;

                SwitchButtons(false);

                try
                {
                    await Task.Run(() => BuildTreeView(Path, strSearch, Token), Token);
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("Ошибка!");
                }
                finally
                {
                    stopwatch.Stop();
                    timer.Stop();
                    lblTime.Text = $"{stopwatch.Elapsed.TotalSeconds:F2}";
                    SwitchButtons(true);
                }
            }
        }

        private async void btnBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.SelectedPath = Properties.Settings.Default.LastSelectedPath;

            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                string Path = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.LastSelectedPath = Path;

                _cancellationToken = new CancellationTokenSource();
                var Token = _cancellationToken.Token;

                SwitchButtons(false);

                try
                {
                    await Task.Run(() => BuildTreeView(Path, "*.*", Token), Token);
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("Ошибка!");
                }
                finally
                {
                    SwitchButtons(true);
                }
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _cancellationToken?.Cancel();
        }

        private void BuildTreeView(string path, string search)
        {
            BuildTreeView(path, search, CancellationToken.None);
        }

        private void BuildTreeView(string path, string search, CancellationToken token)
        {
            try
            {
                Invoke(new Action(() => treeView.Nodes.Clear()));
                DirectoryInfo Dir = new DirectoryInfo(path);
                TreeNode Node = new TreeNode(Dir.Name) { Tag = Dir };
                Invoke(new Action(() => treeView.Nodes.Add(Node)));

                int AllCount = 0;
                int FoundCount = 0;
                PopulateTreeView(Node, search, ref AllCount, ref FoundCount, token);

                Invoke(new Action(() => lblFoundCount.Text = $"Найдено файлов: {FoundCount}/{AllCount}"));
                Invoke(new Action(() => Node.Expand()));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void PopulateTreeView(TreeNode node, string search, ref int allCount, ref int foundCount, CancellationToken token)
        {
            DirectoryInfo ParentDir = (DirectoryInfo)node.Tag;
            try
            {
                foreach (var dir in ParentDir.GetDirectories())
                {
                    token.ThrowIfCancellationRequested();

                    TreeNode DirNode = new TreeNode(dir.Name) { Tag = dir };
                    Invoke(new Action(() => node.Nodes.Add(DirNode)));
                    PopulateTreeView(DirNode, search, ref allCount, ref foundCount, token);
                }

                foreach (var file in ParentDir.GetFiles(search))
                {
                    token.ThrowIfCancellationRequested();

                    TreeNode FileNode = new TreeNode(file.Name) { Tag = file };
                    Invoke(new Action(() => node.Nodes.Add(FileNode)));
                    foundCount++;
                }

                allCount += ParentDir.GetFiles().Length;
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception) { }
        }

        private void SwitchButtons(bool IsFree)
        {
            btnBrowse.Enabled = IsFree;
            btnSearch.Enabled = IsFree;
            btnStop.Enabled = !IsFree;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            lblTime.Text = $"{stopwatch.Elapsed.TotalSeconds:F2}";
        }
    }
}