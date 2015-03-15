﻿using Eto;
using Eto.Drawing;
using Eto.Forms;
using Jojatekok.MoneroAPI;
using Jojatekok.MoneroAPI.Extensions;
using Jojatekok.MoneroAPI.Extensions.Settings;
using Jojatekok.MoneroAPI.Settings;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Jojatekok.MoneroGUI
{
    static class Utilities
    {
        public const string DefaultLanguageCode = "default";

        public const byte FontSize1 = 10;
        public const byte FontSize2 = 12;
        public const byte FontSize3 = 20;

        public const byte Padding1 = 3;
        public const byte Padding2 = 5;
        public const byte Padding3 = 8;
        public const byte Padding4 = 10;
        public const byte Padding5 = 14;
        public const byte Padding6 = 20;
        public const byte Padding7 = 30;

        public static readonly string PathDirectoryThirdPartyLicenses = new DirectoryInfo("Licenses").FullName;
        public static readonly string PathFileLicense = new FileInfo("LICENSE").FullName;

        public static readonly Color ColorSeparator = Color.FromRgb(10526880);
        public static readonly Color ColorStatusBar = Color.FromRgb(15855085);

        public static readonly string[] FileFilterAll = { "*" };
        public static readonly string[] FileFilterPng = { "*.png" };

        public static readonly Size Spacing2 = new Size(Padding2, Padding2);
        public static readonly Size Spacing3 = new Size(Padding3, Padding3);
        public static readonly Size Spacing5 = new Size(Padding5, Padding5);
        public static readonly Size Spacing6 = new Size(Padding6, Padding6);

        public static readonly BindingCollection BindingsToAccountAddress = new BindingCollection();
        public static readonly BindingCollection BindingsToAccountBalance = new BindingCollection();
        public static readonly BindingCollection BindingsToAccountTransactions = new BindingCollection();

        public static readonly Assembly ApplicationAssembly = Assembly.GetExecutingAssembly();
        public static readonly AssemblyName ApplicationAssemblyName = ApplicationAssembly.GetName();
        public static readonly string ApplicationAssemblyNameName = ApplicationAssembly.GetName().Name;

        public static readonly Version ApplicationVersionComparable = ApplicationAssemblyName.Version;
        public const string ApplicationVersionExtra = null;
        public static readonly string ApplicationVersionString = ApplicationVersionComparable.ToString(3) + (ApplicationVersionExtra != null ? "-" + ApplicationVersionExtra : null);

        public static readonly string ApplicationBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        public static readonly Clipboard Clipboard = new Clipboard();

        public static readonly ImageConverter ImageConverter = new ImageConverter();
        public static readonly System.Drawing.ImageConverter SystemImageConverter = new System.Drawing.ImageConverter();

        public static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public static float DefaultFontSize { get; private set; }
        public static FontFamily DefaultFontFamily { get; private set; }

        public static SynchronizationContext SyncContextMain { get; set; }

        public static MoneroProcessManager MoneroProcessManager { get; private set; }
        public static MoneroRpcManager MoneroRpcManager { get; private set; }

        public static readonly FilterCollection<Transaction> DataSourceAccountTransactions = new FilterCollection<Transaction>();
        public static FilterCollection<SettingsManager.ConfigElementContact> DataSourceAddressBook { get; private set; }

        // TODO: Fetch this list from the web
        public static readonly HashSet<string> DataSourceExchangeAddresses = new HashSet<string> {
            // Bittrex
            "463tWEBn5XZJSxLU6uLQnQ2iY9xuNcDbjLSjkn3XAXHCbLrTTErJrBWYgHJQyrCwkNgYvyV3z8zctJLPCZy24jvb3NiTcTJ",

            // Bter
            "47CunEQ4v8FPVNnw9mDgNZeaiSo6SVDydB3AZM341ZtdYpBYNmYeqhh4mpU1X6RSmgBTfC8xqaAtUGC2DArotyaKSz1LJyj",

            // HitBTC
            "45VChYXEMP6HhzHzkcZXdJWXazQNRqy8ZKM3zSTiovzbAbhM7P3zQsY3kFjtCNfX9x2Wy9NRRKcxv9M249hUV4bQG8uaD2c",

            // Poloniex
            "47sghzufGhJJDQEbScMCwVBimTuq6L5JiRixD8VeGbpjCTA12noXmi4ZyBZLc99e66NtnKff34fHsGRoyZk3ES1s1V4QVcB"
        };

        public static void Initialize()
        {
            var defaultFont = new Font(SystemFont.Default);
            DefaultFontSize = defaultFont.Size;
            DefaultFontFamily = defaultFont.Family;

            SyncContextMain = SynchronizationContext.Current;
            using (var button = new Button()) {
                var handler = button.Handler;

                var fieldInfo = handler.GetType().GetField("MinimumSize");
                if (fieldInfo != null) {
                    var size = (Size)(fieldInfo.GetValue(null));
                    size.Width = 0;
                    fieldInfo.SetValue(null, size);

                } else {
                    fieldInfo = handler.GetType().GetField("MinimumWidth");
                    if (fieldInfo != null) {
                        fieldInfo.SetValue(null, 0);
                    }
                }
            }

            SettingsManager.Initialize();

            var storedPathSettings = SettingsManager.Paths;
            var daemonProcessSettings = new DaemonProcessSettings {
                SoftwareDaemon = storedPathSettings.SoftwareDaemon,
                DirectoryDaemonData = storedPathSettings.DirectoryDaemonData,
            };
            var accountManagerProcessSettings = new AccountManagerProcessSettings {
                SoftwareAccountManager = storedPathSettings.SoftwareAccountManager,
                DirectoryAccountBackups = storedPathSettings.DirectoryAccountBackups,
                FileAccountData = storedPathSettings.FileAccountData,
            };

            var storedNetworkSettings = SettingsManager.Network;
            var rpcSettings = new RpcSettings(
                storedNetworkSettings.RpcUrlHostDaemon,
                storedNetworkSettings.RpcUrlPortDaemon,
                storedNetworkSettings.RpcUrlHostAccountManager,
                storedNetworkSettings.RpcUrlPortAccountManager
            );
            if (storedNetworkSettings.IsProxyEnabled) {
                if (!string.IsNullOrEmpty(storedNetworkSettings.ProxyHost) && storedNetworkSettings.ProxyPort != null) {
                    rpcSettings.Proxy = new WebProxy(storedNetworkSettings.ProxyHost, (int)storedNetworkSettings.ProxyPort);
                }
            }

            MoneroProcessManager = new MoneroProcessManager(rpcSettings, accountManagerProcessSettings, daemonProcessSettings);
            MoneroRpcManager = new MoneroRpcManager(rpcSettings);

            DataSourceAddressBook = new FilterCollection<SettingsManager.ConfigElementContact>(SettingsManager.AddressBook.Elements);
            DataSourceAddressBook.CollectionChanged += OnDataSourceAddressBookCollectionChanged;
        }

        static void OnDataSourceAddressBookCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Save the collection's changes into the configuration file

            if (DataSourceAddressBook.Count == 0) {
                SettingsManager.AddressBook.Elements.Clear();
                return;
            }

            var oldItems = e.OldItems;
            if (oldItems != null) {
                for (var i = oldItems.Count - 1; i >= 0; i--) {
                    SettingsManager.AddressBook.Elements.Remove(oldItems[i] as SettingsManager.ConfigElementContact);
                }
            }

            var newItems = e.NewItems;
            if (newItems != null) {
                for (var i = newItems.Count - 1; i >= 0; i--) {
                    SettingsManager.AddressBook.Elements.Add(newItems[i] as SettingsManager.ConfigElementContact);
                }
            }
        }

        public static string GetAbsolutePath(string input)
        {
            return new FileInfo(input).FullName;
        }

        public static string GetRelativePath(string input)
        {
            var inputUri = new Uri(input);
            var applicationBaseDirectoryUri = new Uri(ApplicationBaseDirectory);
            return Uri.UnescapeDataString(applicationBaseDirectoryUri.MakeRelativeUri(inputUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        public static Image LoadImage(string resourceName)
        {
            return ImageConverter.ConvertFrom(
                ImageConverter.ResourcePrefix +
                "Jojatekok.MoneroGUI." + resourceName + ".png," +
                ApplicationAssemblyNameName
            ) as Image;
        }

        public static Label CreateLabel(Func<string> textBinding, HorizontalAlign horizontalAlignment = HorizontalAlign.Left, VerticalAlign verticalAlignment = VerticalAlign.Middle, Font font = null)
        {
            var label = new Label {
                HorizontalAlign = horizontalAlignment,
                VerticalAlign = verticalAlignment
            };

            label.SetTextBindingPath(textBinding);
            if (font != null) label.Font = font;

            return label;
        }

        public static Label CreateLabel<T>(T dataContext, Expression<Func<T, string>> textBinding, HorizontalAlign horizontalAlignment = HorizontalAlign.Left, VerticalAlign verticalAlignment = VerticalAlign.Middle, Font font = null)
        {
            var label = new Label {
                DataContext = dataContext,
                HorizontalAlign = horizontalAlignment,
                VerticalAlign = verticalAlignment
            };

            label.TextBinding.BindDataContext(textBinding);
            if (font != null) label.Font = font;

            return label;
        }

        public static TextBox CreateTextBox(string text, Func<string> placeholderTextBinding = null, Font font = null)
        {
            var textBox = new TextBox {
                Text = text
            };

            if (placeholderTextBinding != null) textBox.SetPlaceholderTextBindingPath(placeholderTextBinding);
            if (font != null) textBox.Font = font;

            return textBox;
        }

        public static TextBox CreateTextBox<T>(T dataContext, Expression<Func<T, string>> textBinding, Func<string> placeholderTextBinding = null, Font font = null)
        {
            var textBox = new TextBox {
                DataContext = dataContext
            };

            textBox.TextBinding.BindDataContext(textBinding);
            if (placeholderTextBinding != null) textBox.SetPlaceholderTextBindingPath(placeholderTextBinding);
            if (font != null) textBox.Font = font;

            return textBox;
        }

        public static Button CreateButton(Func<string> textBinding, Func<string> toolTipBinding, Image image = null, Action onClick = null)
        {
            var button = new Button {
                Image = image
            };

            if (textBinding != null) button.SetTextBindingPath(textBinding);
            if (toolTipBinding != null) button.SetToolTipBindingPath(toolTipBinding);
            if (onClick != null) button.Click += (sender, e) => onClick();

            return button;
        }

        public static NumericUpDown CreateNumericUpDown<T>(T dataContext, Expression<Func<T, double>> valueBinding, int decimalPlaces = MoneroAPI.Utilities.CoinDisplayValueDecimalPlaces, double increment = 0.001, double maxValue = 18446744.0737095, double minValue = 0)
        {
            var numericUpDown = new NumericUpDown {
                DataContext = dataContext,
                DecimalPlaces = decimalPlaces,
                Increment = increment,
                MaxValue = maxValue,
                MinValue = minValue
            };

            numericUpDown.ValueBinding.BindDataContext(valueBinding);

            if (decimalPlaces == 0) {
                numericUpDown.ValueChanged += (sender, e) => {
                    numericUpDown.Value = valueBinding.Compile().Invoke(dataContext);
                };
            }

            return numericUpDown;
        }

        public static GridView CreateGridView<T>(FilterCollection<T> dataStore, params GridColumn[] columns) where T : class
        {
            var gridView = new GridView<T> {
                DataStore = dataStore,
                ShowCellBorders = true
            };

            dataStore.Change = () => gridView.SelectionPreserver;

            for (var i = 0; i < columns.Length; i++) {
                gridView.Columns.Add(columns[i]);
            }

            return gridView;
        }

        public static T GetAssemblyAttribute<T>() where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(ApplicationAssembly, typeof(T), false);
        }
    }
}
