using MyLib.File;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace KakuyomToEpub {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private class TocInfo {
            public string Title;
            public string Page;
            public int Level;
            public TocInfo(int level, string title, int page) {
                this.Level = level;
                this.Title = title;
                this.Page = page.ToString("000");
            }
        }

        #region Constructor
        public MainWindow() {
            InitializeComponent();
        }
        #endregion

        #region Event
        private void Window_Loaded(object sender, RoutedEventArgs e) {
            this.cOutput.Text = KakuyomuToEpub.Properties.Settings.Default.Output;
            this.cTitle.Text = KakuyomuToEpub.Properties.Settings.Default.Title;
        }


        private void Window_DragEnter(object sender, DragEventArgs e) {
            if (0 == this.cOutput.Text.Length ||
                !new DirectoryOperator(this.cOutput.Text).Exists()) {
                e.Effects = DragDropEffects.None;
                MessageBox.Show("output dir is invalid", "error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effects = DragDropEffects.Copy;
            } else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
                return;
            }
            KakuyomuToEpub.Properties.Settings.Default.Output = this.cOutput.Text;
            KakuyomuToEpub.Properties.Settings.Default.Title = this.cTitle.Text;
            KakuyomuToEpub.Properties.Settings.Default.Save();

            try {
                this.cContainer.Visibility = Visibility.Collapsed;
                this.cProgress.Visibility = Visibility.Visible;
                this.cProgress.Content = "delete outpu files";
                this.DoEvents();

                this.DeleteAllFiles();

                var srcText = (((string[])e.Data.GetData(DataFormats.FileDrop))[0]);
                var tocs = new List<TocInfo>();
                using (var src = new FileOperator(srcText)) {
                    src.OpenForRead(System.Text.Encoding.UTF8);
                    var index = 0;
                    var hasBlank = false;
                    var isFirst = false;
                    FileOperator dest = null;
                    while (!src.Eof) {
                        var line = src.ReadLine().Trim();
                        if (line.EndsWith("は大見出し］")) {
                            var pos = line.IndexOf("「");
                            var title = line.Substring(pos + 1);
                            pos = title.LastIndexOf("」");
                            title = title.Substring(0, pos);
                            tocs.Add(new TocInfo(1, title, index));
                            continue;
                        }
                        if (line == "［＃改ページ］") {
                            continue;
                        }
                        if (line.EndsWith("は中見出し］")) {
                            index++;
                            if (null != dest) {
                                dest.Write("</body>");
                                dest.Write("</html>");
                                dest.Close();
                            }
                            dest = this.CreateFile(index);

                            var pos = line.IndexOf("「");
                            var title = line.Substring(pos+1);
                            pos = title.LastIndexOf("」");
                            title = title.Substring(0, pos );
                            tocs.Add(new TocInfo(2, title, index));
                            dest.WriteLine($"<h1>{this.ConvertText(title, true)}</h1>");
                            hasBlank = false;
                            isFirst = true;
                            continue;
                        }
                        if (line.Length == 0) {
                            hasBlank = true;
                            continue;
                        }
                        if (hasBlank && !isFirst) {
                            dest.WriteLine("<div class=\"spacer\" />");
                        }
                        hasBlank = false;
                        isFirst = false;

                        if (line.StartsWith("「")) {
                            dest.Write("<p class=\"no-indent\">");
                        } else {
                            dest.Write("<p>");
                        }
                        dest.Write(this.ConvertText(this.SetRuby(line)) + "</p>\n");
                    }
                    index++;
                    if (null != dest) {
                        dest.Write("</body>");
                        dest.Write("</html>");
                        dest.Close();
                    }

                    this.CreateToc(tocs);
                    
                }

            } finally {
                this.cContainer.Visibility = Visibility.Visible;
                this.cProgress.Visibility = Visibility.Collapsed;
                this.cProgress.Content = "";
                this.DoEvents();
            }

        }

        #endregion

        #region Private Method
        /// <summary>
        /// doevents
        /// </summary>
        private void DoEvents() {
            DispatcherFrame frame = new DispatcherFrame();
            var callback = new DispatcherOperationCallback(obj =>
            {
                ((DispatcherFrame)obj).Continue = false;
                return null;
            });
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, callback, frame);
            Dispatcher.PushFrame(frame);
        }

        /// <summary>
        /// delete all files in output dir
        /// </summary>
        private void DeleteAllFiles() {
            var files = System.IO.Directory.GetFiles(this.cOutput.Text);
            foreach(var file in files) {
                System.IO.File.Delete(file);
            }
        }

        /// <summary>
        /// create a page xhtml
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private FileOperator CreateFile(int index) {
            var dest = new FileOperator(this.cOutput.Text + $@"\p-{index.ToString("000")}.xhtml");
            dest.OpenForWrite(System.Text.Encoding.UTF8);
            dest.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            dest.WriteLine("<!DOCTYPE html>");
            dest.WriteLine("<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"ja\" class=\"vrtl\">");
            dest.WriteLine("<head>");
            dest.WriteLine("	<meta charset=\"UTF-8\" />");
            dest.WriteLine("	<title>" + this.cTitle.Text + "</title>");
            dest.WriteLine("	<link rel=\"stylesheet\" type=\"text/css\" href =\"../style/book-style.css\" />");
            dest.WriteLine("</head>");
            dest.WriteLine("<body>");
            return dest;
        }

        /// <summary>
        /// create a page xhtml
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private void CreateToc(List<TocInfo> tocs) {
            using (var dest = new FileOperator(this.cOutput.Text + $@"\..\navigation-documents.xhtml")) {
                dest.OpenForWrite(System.Text.Encoding.UTF8);
                dest.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                dest.WriteLine("<!DOCTYPE html>");
                dest.WriteLine("<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"ja\" class=\"hltr\">");
                dest.WriteLine("<head>");
                dest.WriteLine("	<meta charset=\"UTF-8\" />");
                dest.WriteLine("	<title>" + this.cTitle.Text + "</title>");
                dest.WriteLine("	<link rel=\"stylesheet\" type=\"text/css\" href =\"./style/book-style.css\" />");
                dest.WriteLine("</head>");
                dest.WriteLine("<body>");
                dest.WriteLine("<nav epub:type=\"toc\" id=\"toc\">");
                dest.WriteLine("<h1>目次</h1>");

                bool isFirst = true;
                // 大見出しの直後は必ず中見出しである前提
                dest.WriteLine("<ol>");
                foreach (var toc in tocs) {
                    if (toc.Level == 1) {
                        if (isFirst) {
                            isFirst = false;
                        } else {
                            dest.WriteLine($"      </ol>");
                            dest.WriteLine($"   </li>");
                        }
                        dest.WriteLine($"    <li><span>{toc.Title}</span>");
                        dest.WriteLine($"      <ol>");
                        continue;
                    } 
                    dest.WriteLine($"      <li><a href=\"xhtml/p-{toc.Page}.xhtml\">{this.ConvertText(toc.Title, true)}</a></li>");
                }
                dest.WriteLine("  </ol>");
                dest.WriteLine("  </li>");
                dest.WriteLine("</ol>");

                dest.WriteLine("</nav>");
                dest.WriteLine("</body>");
                dest.WriteLine("</html>");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private string SetRuby(string line) {
            var pos = line.IndexOf("｜");
            if (pos < 0) {
                return line;
            }

            var tmp = line;
            var result = new StringBuilder();
            while (-1 < pos) {
                result.Append(tmp.Substring(0, pos));
                tmp = tmp.Substring(pos + 1);

                pos = tmp.IndexOf("《");
                var word = tmp.Substring(0, pos);
                tmp = tmp.Substring(pos + 1);

                pos = tmp.IndexOf("》");
                var ruby = tmp.Substring(0, pos);
                tmp = tmp.Substring(pos + 1);

                var count = new int[word.Length];
                for (int i=0; i < ruby.Length; i++) {
                    count[i % word.Length]++;
                }

                result.Append("<ruby>");
                for(int i=0; i < word.Length; i++) {
                    result.Append(word.Substring(i, 1));
                    result.Append("<rt>");
                    result.Append(ruby.Substring(0, count[i]));
                    ruby = ruby.Substring(count[i]);
                    result.Append("</rt>");
                }
                result.Append("</ruby>");
                pos = tmp.IndexOf("｜");
            }
            result.Append(tmp);
            return result.ToString();
        }

        private string ConvertText(string line,  bool skipEnd = false) {
            //var baseText =  line
            //    .Replace("0", "０")
            //    .Replace("1", "１")
            //    .Replace("2", "２")
            //    .Replace("3", "３")
            //    .Replace("4", "４")
            //    .Replace("5", "５")
            //    .Replace("6", "６")
            //    .Replace("7", "７")
            //    .Replace("8", "８")
            //    .Replace("9", "９")
            //    .Replace("?", "？")
            //    .Replace("&", "&amp;");
            var baseText = line
                .Replace("0", "〇")
                .Replace("1", "一")
                .Replace("2", "二")
                .Replace("3", "三")
                .Replace("4", "四")
                .Replace("5", "五")
                .Replace("6", "六")
                .Replace("7", "七")
                .Replace("8", "八")
                .Replace("9", "九")
                .Replace("０", "〇")
                .Replace("１", "一")
                .Replace("２", "二")
                .Replace("３", "三")
                .Replace("４", "四")
                .Replace("５", "五")
                .Replace("６", "六")
                .Replace("７", "七")
                .Replace("８", "八")
                .Replace("９", "九")
                .Replace("?", "？")
                .Replace(",", "、")
                .Replace("，", "、")
                .Replace("!", "！")
                .Replace("?", "？")
                .Replace("&", "&amp;")
                .Replace("！！", "<span class=\"tcy\">!! </span>")
                .Replace("！？", "<span class=\"tcy\">!? </span>")
                .Replace("？？", "<span class=\"tcy\">?? </span>")
                .Replace(" 」", "」")
                .Replace(" 』", "』")
                ;



            baseText = baseText.Replace("［＃丸傍点］","<ruby>");
            baseText = baseText.Replace("［＃丸傍点終わり］", "<rt>・</rt></ruby>");
            

            //if (!skipEnd && !baseText.EndsWith("。")) {
            //    if (baseText.EndsWith("」") ||
            //        baseText.EndsWith("』") ||
            //        baseText.EndsWith("！") ||
            //        baseText.EndsWith("？") ||
            //        baseText.EndsWith("＞") ||
            //        baseText.EndsWith("◇"))
            //        {
            //        // nop
            //    } else {
            //        baseText = baseText + "。";
            //    }
            //}
   
            return baseText;
        }
        #endregion

    }
}


