using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Socket_project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Service ss;
        private Window1 window1;
        private Window2 window2;


        public MainWindow()
        {
            InitializeComponent();
            ShowOtherWindows();
            start_server.Click += StartServer_Click;
            stop_listen.Click += StopListen_Click;
        }

        private void ShowOtherWindows()
        {
            window1=new Window1();
            window1.Show();
            window2 =new Window2();
            window2.Show();
        }
        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            int portNumber = int.Parse(port.Text);
            ss = new Service(IP.Text, portNumber);
            ss.Start();
            message.Content = "Server staeted on" + IP.Text + ":" + port.Text;
        }
       
        private void StopListen_Click(object sender, RoutedEventArgs e)
        {
            if (ss != null)
            {
                ss.Stop();
                message.Content = "Server stopped listening.";
            }
            else
            {
                message.Content = "Server is not running.";
            }
        }

    }
    public class Service
    {
        private Socket socketService;
        private List<Socket> userList;
        private Dictionary<Socket, string> usernameBySocket; // Store user names
        private bool isRunning;

        public Service(string ip, int port)
        {
            // Initialize socket with options
            socketService = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketService.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socketService.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
            userList = new List<Socket>();
            usernameBySocket = new Dictionary<Socket, string>();
            isRunning = false;
        }

        public void Start()
        {
            try
            {
                if (!isRunning)
                {
                    socketService.Listen(10);
                    isRunning = true;
                    Console.WriteLine("Server started successfully");

                    Thread accThread = new Thread(Accept);
                    accThread.IsBackground = true;
                    accThread.Start();
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("SocketException in Start: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting server: " + ex.Message);
            }
        }

        public void Stop()
        {
            try
            {
                isRunning = false; // Ensure that Accept loop exits

                // Close all client connections
                foreach (var client in userList.ToList())
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
                userList.Clear();

                // Close the server socket
                socketService?.Shutdown(SocketShutdown.Both);
                socketService?.Close();
                socketService = null; // Avoid reusing the socket

                Console.WriteLine("Server stopped.");
            }
            catch (SocketException ex)
            {
                Console.WriteLine("SocketException in Stop: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error stopping server: " + ex.Message);
            }
        }

        private void Accept()
        {
            while (isRunning)
            {
                try
                {
                    Socket clientSocket = socketService.Accept();
                    userList.Add(clientSocket);

                    Thread recvThread = new Thread(ReceMessage);
                    recvThread.IsBackground = true;
                    recvThread.Start(clientSocket);
                }
                catch (SocketException ex)
                {
                    if (isRunning) // Only print the error if the server is running
                    {
                        Console.WriteLine("SocketException in Accept: " + ex.Message);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in Accept: " + ex.Message);
                    break;
                }
            }

            // Clean up remaining connections
            foreach (var client in userList)
            {
                client.Close();
            }
            userList.Clear();
        }

        private void ReceMessage(object obj)
        {
            Socket client = obj as Socket;
            byte[] strByte = new byte[1024 * 1024];
            string str = "";

            try
            {
                while (true)
                {
                    int len = client.Receive(strByte);
                    if (len == 0) break; // Handle client disconnection

                    str = Encoding.UTF8.GetString(strByte, 0, len);
                    if (str.StartsWith("user connect:"))
                    {
                        string username = str.Substring("user connect:".Length);
                        usernameBySocket[client] = username; // 保存用户名
                        string successMessage = $"{username} successfully connected";
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ((MainWindow)Application.Current.MainWindow).message.Content += "\n" + successMessage;
                        });
                    }
                    else if (str.StartsWith("user disconnect:"))
                    {
                        string username = str.Substring("user disconnect:".Length);
                        string disconnectMessage = $"{username} successfully disconnected";
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ((MainWindow)Application.Current.MainWindow).message.Content += "\n" + disconnectMessage;
                        });
                    }
                    else
                    {
                        // Broadcast message with username
                        string username = GetUsernameBySocket(client);
                        if (username != null)
                        {
                            str = $"{username}: {str}";
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ((MainWindow)Application.Current.MainWindow).message.Content += "\n" + str;
                        });
                        Broadcast(str, client);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(IPToAddress(client) + " exited with error: " + ex.Message);
            }
            finally
            {
                string username = GetUsernameBySocket(client);
                if (username != null)
                {
                    string disconnectMessage = $"{username} is disconnect";
                    Broadcast(disconnectMessage, client);
                }

                userList.Remove(client);
                usernameBySocket.Remove(client);
                client.Close();
            }
        }

        private string GetUsernameBySocket(Socket socket)
        {
            return usernameBySocket.ContainsKey(socket) ? usernameBySocket[socket] : null;
        }

        private void Broadcast(string message, Socket sender)
        {
            foreach (Socket client in userList)
            {
                if (client != sender)
                {
                    try
                    {
                        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                        client.Send(messageBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error broadcasting message: " + ex.Message);
                        userList.Remove(client);
                        client.Close();
                    }
                }
            }
        }

        private string IPToAddress(Socket socket)
        {
            IPEndPoint endPoint = socket.RemoteEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "Unknown";
        }
    }



}