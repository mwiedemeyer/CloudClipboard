using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using System.Collections.Specialized;
using System.Configuration;
using System.Drawing.Imaging;

namespace CloudClipboard;

public partial class FormMain : Form
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly ServiceBusClient _sbClient;
    private readonly ServiceBusProcessor _sbProcessor;
    private readonly string _localName;
    private readonly string _remoteName;
    private readonly ServiceBusSender _sbSender;
    private bool _isProcessing = false;

    public FormMain()
    {
        InitializeComponent();

        _localName = ConfigurationManager.AppSettings["LocalName"];
        _remoteName = ConfigurationManager.AppSettings["RemoteName"];

        _blobContainerClient = new BlobContainerClient(ConfigurationManager.AppSettings["StorageConnectionString"], ConfigurationManager.AppSettings["StorageContainer"]);

        _sbClient = new ServiceBusClient(ConfigurationManager.AppSettings["ServiceBusConnectionString"],
            new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets, Identifier = _localName });

        _sbProcessor = _sbClient.CreateProcessor(_remoteName, new ServiceBusProcessorOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        _sbProcessor.ProcessMessageAsync += ProcessMessageAsync;
        _sbProcessor.ProcessErrorAsync += ProcessErrorAsync;
        _sbProcessor.StartProcessingAsync();

        _sbSender = _sbClient.CreateSender(_localName);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs arg) => Task.CompletedTask;

    private async Task ProcessMessageAsync(ProcessMessageEventArgs arg)
    {
        _isProcessing = true;
        var content = arg.Message.Body.ToString();

        // if message content is %$IMAGE, download image from blob storage and set to clipboard
        if (content == "%$IMAGE")
        {
            try
            {
                var blob = _blobContainerClient.GetBlobClient($"image-{_remoteName}.bmp");
                var imgContent = (await blob.DownloadContentAsync()).Value.Content;
                using var img = Image.FromStream(imgContent.ToStream());
                Invoke(() =>
                {
                    Clipboard.SetImage(img);
                });
            }
            catch (Exception)
            {
                // ignore
            }
        }
        // if message content starts with %$FILE$, download files from blob storage and set to clipboard
        else if (content.StartsWith($"%$FILE$"))
        {
            try
            {
                var filenames = content.Replace($"%$FILE$", string.Empty);

                var filePaths = new StringCollection();
                foreach (var file in filenames.Split('$', StringSplitOptions.RemoveEmptyEntries))
                {
                    var blob = _blobContainerClient.GetBlobClient(file);
                    var filepath = Path.Combine(Path.GetTempPath(), file);
                    await blob.DownloadToAsync(filepath);
                    filePaths.Add(filepath);
                }
                Invoke(() =>
                {
                    Clipboard.SetFileDropList(filePaths);
                });
            }
            catch (Exception)
            {
                // ignore
            }
        }
        // else set text to clipboard
        else
        {
            Invoke(() =>
            {
                Clipboard.SetText(content);
            });
        }

        // add a delay to avoid clipboard event flood
        await Task.Delay(500);
        _isProcessing = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE)
        {
            if (_isProcessing)
            {
                return;
            }

            try
            {
                if (Clipboard.ContainsText())
                {
                    // text
                    var text = Clipboard.GetText();
                    _sbSender.SendMessageAsync(new ServiceBusMessage
                    {
                        Body = new BinaryData(text)
                    }).Wait();
                }
                else if (Clipboard.ContainsImage())
                {
                    // image
                    var img = Clipboard.GetImage();
                    var blob = _blobContainerClient.GetBlobClient($"image-{_localName}.bmp");
                    using var ms = new MemoryStream();
                    img.Save(ms, ImageFormat.Bmp);
                    ms.Position = 0;
                    blob.Upload(ms, true);
                    _sbSender.SendMessageAsync(new ServiceBusMessage
                    {
                        Body = new BinaryData("%$IMAGE")
                    }).Wait();
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    // one or more files
                    var files = Clipboard.GetFileDropList();
                    var filenames = string.Empty;
                    foreach (var file in files)
                    {
                        var filename = Path.GetFileName(file);
                        var blob = _blobContainerClient.GetBlobClient(filename);
                        blob.Upload(file, true);
                        filenames += $"{filename}$";
                    }

                    _sbSender.SendMessageAsync(new ServiceBusMessage
                    {
                        Body = new BinaryData($"%$FILE${filenames}")
                    }).Wait();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        base.WndProc(ref m);
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        ClipboardMethods.AddClipboardFormatListener(Handle);
        Hide();
    }

    private void Form1_FormClosed(object sender, FormClosedEventArgs e)
    {
        ClipboardMethods.RemoveClipboardFormatListener(Handle);
    }
}
