using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Network_Monitor.Properties;

namespace Network_Monitor
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private string _download;
        private long _lastDownload;
        private long _lastLatency = -1;
        private long _lastUpload = -1;
        private string _latency;
        private IPStatus _latencyStatus;
        private string _upload;

        public MainWindowViewModel()
        {
            StartLatencyReader();
        }

        public string Latency
        {
            get { return _latency; }
            private set { Set(ref _latency, value); }
        }

        public IPStatus LatencyStatus
        {
            get { return _latencyStatus; }
            private set { Set(ref _latencyStatus, value); }
        }

        public string Download
        {
            get { return _download; }
            private set { Set(ref _download, value); }
        }

        public string Upload
        {
            get { return _upload; }
            private set { Set(ref _upload, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool Set<T>(ref T field, T newValue = default(T), bool checkEquality = true,
            [CallerMemberName] string propertyName = null)
        {
            if (checkEquality && EqualityComparer<T>.Default.Equals(field, newValue))
            {
                return false;
            }
            field = newValue;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void StartLatencyReader()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (true)
                {
                    var reply = NetworkHelper.GetLatency();
                    if (reply != null)
                    {
                        var isFirstCheck = _lastLatency == -1;

                        var status = reply.Status;
                        var latency = reply.RoundtripTime;
                        var downloadedBytes = NetworkHelper.GetDownloadedBytes();
                        var uploadedBytes = NetworkHelper.GetUploadedBytes();
                        var downloadDifference = downloadedBytes - _lastDownload;
                        var uploadDifference = uploadedBytes - _lastUpload;
                        _lastLatency = latency;
                        _lastDownload = downloadedBytes;
                        _lastUpload = uploadedBytes;
                        if (!isFirstCheck)
                        {
                            Application.Current.Dispatcher.BeginInvoke(
                                DispatcherPriority.Background,
                                new Action(() =>
                                {
                                    Latency = status == IPStatus.Success ? latency.ToString() : "Error";
                                    LatencyStatus = status;
                                    Download = ByteHelper.BytesToString(downloadDifference);
                                    Upload = ByteHelper.BytesToString(uploadDifference);
                                }));
                        }
                    }
                    Thread.Sleep(Settings.Default.Interval);
                }
            }).Start();
        }
    }
}