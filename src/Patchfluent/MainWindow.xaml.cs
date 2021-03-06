﻿//
// Copyright 2016 Vyacheslav Napadovsky.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

/*
 Forked as Patchfluent
 Copyright 2020, Builtbybel (www.builtbybel.com)
 For ref. take a look at the scripting example provided by Microsoft https://docs.microsoft.com/en-us/windows/win32/wua_sdk/searching--downloading--and-installing-updates
 */

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Patchfluent
{
    public partial class MainWindow
    {
        // App update
        private readonly string _releaseURL = "https://raw.githubusercontent.com/builtbybel/patchfluent/master/latest.txt";

        internal static string GetCurrentVersionTostring() => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public Version CurrentVersion = new Version(GetCurrentVersionTostring());
        public Version LatestVersion;

        // Windows update
        private dynamic _updateSession = null;

        private dynamic _updateSearcher = null;
        private dynamic _searchResult = null;

        private void SearchForAppUpdates()
        {
            try
            {
                WebRequest hreq = WebRequest.Create(_releaseURL);
                hreq.Timeout = 10000;
                hreq.Headers.Set("Cache-Control", "no-cache, no-store, must-revalidate");

                WebResponse hres = hreq.GetResponse();
                StreamReader sr = new StreamReader(hres.GetResponseStream());

                LatestVersion = new Version(sr.ReadToEnd().Trim());

                // Done and dispose!
                sr.Dispose();
                hres.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "", MessageBoxButton.OK);   // Update check failed!
            }

            var equals = LatestVersion.CompareTo(CurrentVersion);

            if (equals == 0)
            {
                _appUpdateAvailable.Visibility = Visibility.Hidden;
            }
            else if (equals < 0)
            {
                // MessageBox.Show("You are using an unoffical version of Patchfluent.","",  MessageBoxButton.OK);  // Unofficial
                _appUpdateAvailable.Visibility = Visibility.Hidden;
            }
            else    // New release available!
            {
                _appUpdateAvailable.Visibility = Visibility.Visible;
            }
        }

        private async Task SearchForUpdates()
        {
            _status.Text = "Checking for updates ...";
            _description.Selection.Text = "No update selected";

            await Task.Run(() =>
            {
                _searchResult = _updateSearcher.Search("IsInstalled=0");
            });

            _status.Text = "Updates are available.";
            var list = new List<UpdateItem>();
            int count = _searchResult.Updates.Count;
            _installButton.IsEnabled = count > 0;

            if (count > 0)
            {
                for (int i = 0; i < _searchResult.Updates.Count; ++i)
                    list.Add(new UpdateItem(_searchResult.Updates.Item(i)));
            }
            else
            {
                _status.Text = "Your device is up to date.";
                _statusCurrent.Text = "";
            }
            _list.ItemsSource = list;
        }

        protected override async void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (_updateSession == null)
            {
                try
                {
                    _updateSession = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
                    _updateSession.ClientApplicationID = "Patchfluent";
                    _updateSearcher = _updateSession.CreateUpdateSearcher();
                    await SearchForUpdates();   // Search for Windows updates
                    SearchForAppUpdates();      // Search for app updates

                    _installButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.ToString(), "Exception has occured");
                    _status.Text = "Windows Update service is not available.";
                    IsEnabled = false;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // GUI options
            // This is using font icons predefined in the fonts of Segoe MDL2 Assets
            _assetHamburger.Content = "\ue700";    // Menu icon
            _assetRefresh.Content = "\uecc5";      // Update icon

            CheckAU();      // Check auto. update status
        }

        /// <summary>
        ///  Check auto. update options
        /// </summary>
        private void CheckAU()
        {
            RegistryKey RegHKLM = Registry.LocalMachine;
            RegistryKey updateRegHKLM;

            if (RegHKLM.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU") != null)
            {
                updateRegHKLM = RegHKLM.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");

                if (updateRegHKLM.GetValueNames().Contains("AUOptions"))
                {
                    if ((Int32)updateRegHKLM.GetValue("AUOptions") == 2) { _checkAU.IsChecked = true; _checkAU.Content = "*Automatic updates on this device are turned off."; }
                    else { _checkAU.IsChecked = false; _checkAU.Content = "*Automatic updates on this device are turned on."; }
                }

                updateRegHKLM.Close();
            }
        }

        /// <summary>
        ///  Configure auto. update options
        /// </summary>
        private void ConfigureAU_Click(object sender, RoutedEventArgs e)
        {
            string key;
            RegistryKey RegHKLM = Registry.LocalMachine;
            if (Environment.Is64BitOperatingSystem) RegHKLM = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            RegistryKey updateRegHKLM = RegHKLM;
            bool itmChk = _checkAU.IsChecked.Value;

            key = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            if (itmChk) { updateRegHKLM.OpenSubKey(key, true).SetValue("AUOptions", 2, RegistryValueKind.DWord); CheckAU(); }  // Disable AU
            else { updateRegHKLM.OpenSubKey(key, true).SetValue("AUOptions", 1, RegistryValueKind.DWord); CheckAU(); }        // Enable
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            _installButton.IsEnabled = false;

            try
            {
                var list = _list.ItemsSource as List<UpdateItem>;
                dynamic updatesToInstall = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
                foreach (var item in list)
                {
                    if (!item.IsChecked)
                        continue;
                    if (!item.EulaAccepted)
                    {
                        if (MessageBox.Show(this, item.Update.EulaText, "Do you accept this license agreement?", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                            continue;
                        item.Update.AcceptEula();
                    }
                    updatesToInstall.Add(item.Update);
                }
                if (updatesToInstall.Count == 0)
                {
                    _status.Text = "No updates are available.";
                }
                else
                {
                    _status.Text = "Downloading updates ...";
                    dynamic downloader = _updateSession.CreateUpdateDownloader();
                    downloader.Updates = updatesToInstall;
                    await Task.Run(() => { downloader.Download(); });

                    for (int i = 0; i < updatesToInstall.Count; ++i)
                    { _status.Text = "Installing updates ... "; _statusCurrent.Text = updatesToInstall.Item(i).Title; }

                    dynamic installer = _updateSession.CreateUpdateInstaller();
                    installer.Updates = updatesToInstall;
                    dynamic installationResult = null;
                    await Task.Run(() => { installationResult = installer.Install(); });

                    var sb = new StringBuilder();
                    if (installationResult.RebootRequired == true)
                        sb.Append("[REBOOT REQUIRED] ");
                    // sb.AppendFormat("Code: {0}\n", installationResult.ResultCode);
                    sb.Append("\n");
                    for (int i = 0; i < updatesToInstall.Count; ++i)
                    {
                        sb.AppendFormat("{1}\n",
                           installationResult.GetUpdateResult(i).ResultCode,
                            updatesToInstall.Item(i).Title);
                    }
                    MessageBox.Show(this, sb.ToString(), "Installed Updates");
                    _description.Document.Blocks.Clear();

                    await SearchForUpdates();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Exception has occured");
            }

            _installButton.IsEnabled = true;
        }

        private void ListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (UpdateItem item in e.AddedItems)
            {
                _description.Document.Blocks.Clear();
                var p = new Paragraph();
                p.Inlines.Add(new Run(item.Description));
                p.EnableHyperlinks();
                _description.Document.Blocks.Add(p);
                break;
            }
        }

        private void _imageGitHub_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("https://github.com/builtbybel/patchfluent");
        }

        private async void _assetRefresh_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await SearchForUpdates();
        }

        private void _linkUpdateHistory_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("ms-settings:windowsupdate-history");
        }

        private void _linkUpdateOptional_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("ms-settings:windowsupdate-seekerondemand");
        }

        private void _linkUpdateAdvanced_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("ms-settings:windowsupdate-options");
        }

        private void NewVersion_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("There is a new version of Patchfluent available " + LatestVersion + "\nYour are using version " + CurrentVersion + "\n\nDo you want to open the @github/releases page?", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes) // New release available!
            {
                Process.Start("https://github.com/builtbybel/patchfluent/releases/tag/" + LatestVersion);
            }
        }
    }

    internal static class RichEditExtensions
    {
        public static void EnableHyperlinks(this Paragraph p)
        {
            string paragraphText = new TextRange(p.ContentStart, p.ContentEnd).Text;
            foreach (string word in paragraphText.Split(' ', '\n', '\t').ToList())
            {
                if (word.IndexOf("//") != -1 && Uri.IsWellFormedUriString(word, UriKind.Absolute))
                {
                    Uri uri = new Uri(word, UriKind.RelativeOrAbsolute);
                    if (!uri.IsAbsoluteUri)
                        uri = new Uri(@"http://" + word, UriKind.Absolute);
                    for (TextPointer position = p.ContentStart;
                        position != null;
                        position = position.GetNextContextPosition(LogicalDirection.Forward))
                    {
                        if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                        {
                            string textRun = position.GetTextInRun(LogicalDirection.Forward);
                            int indexInRun = textRun.IndexOf(word);
                            if (indexInRun >= 0)
                            {
                                TextPointer start = position.GetPositionAtOffset(indexInRun);
                                TextPointer end = start.GetPositionAtOffset(word.Length);
                                var link = new Hyperlink(start, end);
                                link.NavigateUri = uri;
                                link.RequestNavigate += (sender, args) => Process.Start(args.Uri.ToString());
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}

internal class UpdateItem
{
    private readonly dynamic _update;

    private int GetSizeOrder(decimal size)
    {
        int order;
        for (order = 0; size > 1024; ++order)
        {
            size /= 1024;
        }
        return order;
    }

    private string SizeToString(decimal size, int order, bool addsuffix)
    {
        string fmt;
        switch (order)
        {
            case 0: fmt = "{0} B"; break;
            case 1: fmt = "{0} KB"; size /= 1024; break;
            case 2: fmt = "{0} MB"; size /= 1024 * 1024; break;
            default: fmt = "{0} GB"; size /= 1024 * 1024 * 1024; break;
        }
        return addsuffix
            ? string.Format(fmt, (int)size)
            : ((int)size).ToString();
    }

    public UpdateItem(dynamic update)
    {
        _update = update;

        //IsChecked = _update.AutoSelectOnWebSites;     // this line will select them all
        IsChecked = _update.IsMandatory;

        var sb = new StringBuilder();
        if (EulaAccepted == false)
            sb.Append("[EULA NOT ACCEPTED] ");
        sb.AppendFormat("{0}\n", _update.Title);
        if (_update.Description != null)
            sb.AppendFormat("{0}\n", _update.Description);
        if (_update.MoreInfoUrls != null && _update.MoreInfoUrls.Count > 0)
        {
            sb.AppendFormat("More info:\n");
            for (int i = 0; i < _update.MoreInfoUrls.Count; ++i)
                sb.AppendFormat("{0}\n", _update.MoreInfoUrls.Item(i));
        }
        if (_update.EulaText != null)
            sb.AppendFormat("EULA TEXT:\n{0}\n\n", _update.EulaText);
        if (_update.ReleaseNotes != null)
            sb.AppendFormat("Release Notes:\n{0}\n\n", _update.ReleaseNotes);

        dynamic bundle = _update.BundledUpdates;
        if (bundle != null && bundle.Count > 0)
        {
            sb.AppendFormat("This update contains {0} packages:\n", bundle.Count);
            for (int i = 0; i < bundle.Count; ++i)
            {
                var item = new UpdateItem(bundle.Item(i));
                var desc = item.Description;
                desc = desc.Substring(0, desc.Length - 1);
                sb.AppendFormat("#{0}: {1}\n", i + 1, desc.Replace("\n", "\n * "));
            }
        }

        decimal minSize = _update.MinDownloadSize;
        decimal maxSize = _update.MaxDownloadSize;
        string sizeString;
        if (minSize == 0 || minSize == maxSize)
        {
            sizeString = SizeToString(maxSize, GetSizeOrder(maxSize), true);
        }
        else
        {
            int order = Math.Max(GetSizeOrder(minSize), GetSizeOrder(maxSize));
            sizeString = string.Format("{0} - {1}",
                SizeToString(minSize, order, false),
                SizeToString(maxSize, order, true)
            );
        }
        Title = string.Format("{0} ({1})", _update.Title, sizeString);

        Description = sb.ToString();
    }

    public bool IsChecked { get; set; }
    public string Title { get; }
    public string Description { get; }

    public dynamic Update { get { return _update; } }
    public bool EulaAccepted { get { return _update.EulaAccepted; } }
}