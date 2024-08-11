using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Socket_project
{
    public partial class Window1 : Window
    {
        private Socket _clientSocket;
        private Thread _receiveThread;

        public Window1()
        {
            InitializeComponent();
        }
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ipAddress = IP.Text;
                int port = int.Parse(port1.Text);

                IPAddress ip = IPAddress.Parse(ipAddress);
                IPEndPoint endPoint = new IPEndPoint(ip, port);

                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.Connect(endPoint);

                string username = user1.Text;
                string connectMessage = $"user connect:{username}";
                byte[] connectMessageBytes = Encoding.UTF8.GetBytes(connectMessage);
                _clientSocket.Send(connectMessageBytes);

                client_message1.Content = "connected successfully";

                // Start receiving messages
                _receiveThread = new Thread(ReceiveMessages);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

            }
            catch (Exception ex)
            {
                client_message1.Content = $"Connection failed: {ex.Message}";
            }
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            while (true)
            {
                try
                {
                    int receivedBytes = _clientSocket.Receive(buffer);
                    if (receivedBytes > 0)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                        // Append the message to the client_message1 TextBox
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            client_message1.Content += $"\n{receivedMessage}";
                        });
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        client_message1.Content += $"\nError receiving message: {ex.Message}";
                    });
                    break;
                }
            }
        }
        private void Send_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clientSocket != null && _clientSocket.Connected)
                {
                    string message = message1.Text;
                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    _clientSocket.Send(messageBytes);

                    // Also add the message to the local message box
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        client_message1.Content += $"\nYou: {message}";
                    });

                    // Clear the message box
                    message1.Text = string.Empty;
                }
                else
                {
                    client_message1.Content = "Not connected";
                }
            }
            catch
            {
                client_message1.Content = $"Sending message failed";
            }
        }
        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clientSocket != null && _clientSocket.Connected)
                {
                    string username = user1.Text;
                    string disconnectMessage = $"user disconnect:{username}";
                    byte[] disconnectMessageBytes = Encoding.UTF8.GetBytes(disconnectMessage);
                    _clientSocket.Send(disconnectMessageBytes);

                    _clientSocket.Shutdown(SocketShutdown.Both);
                    _clientSocket.Close();
                    client_message1.Content = "Disconnected Successfully";
                }
                else
                {
                    client_message1.Content = "Not connected";
                }
            }
            catch (Exception ex)
            {
                client_message1.Content = "";
            }
        }

        public class Service
        {
            private Socket socketService;
            private List<Socket> userList;

            public Service(string ip, int port)
            {
                socketService = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socketService.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
                userList = new List<Socket>();
            }

            public void Start()
            {
                socketService.Listen(10);
                Console.WriteLine("Server started successfully");

                Thread accThread = new Thread(Accept);
                accThread.IsBackground = true;
                accThread.Start();
            }

            public void Stop()
            {
                socketService.Close();
                Console.WriteLine("Server stopped.");
            }

            private void Accept()
            {
                while (true)
                {
                    try
                    {
                        Socket clientSocket = socketService.Accept();
                        userList.Add(clientSocket);
                        Console.WriteLine(IPToAddress(clientSocket) + " is connected");

                        Thread recvThread = new Thread(ReceMessage);
                        recvThread.IsBackground = true;
                        recvThread.Start(clientSocket);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error in Accept: " + e.Message);
                    }
                }
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

                        // 处理连接成功消息
                        if (str.StartsWith("USER_CONNECTED:"))
                        {
                            string username = str.Substring("USER_CONNECTED:".Length);
                            string successMessage = $"{username} successfully connected";
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ((MainWindow)Application.Current.MainWindow).message.Content += "\n" + successMessage;
                            });
                        }
                        else if (str.StartsWith("USER_DISCONNECTED:"))
                        {
                            string username = str.Substring("USER_DISCONNECTED:".Length);
                            string disconnectMessage = $"{username} successfully disconnected";
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ((MainWindow)Application.Current.MainWindow).message.Content += "\n" + disconnectMessage;
                            });
                        }
                        else
                        {
                            // 处理其他类型的消息
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ((MainWindow)Application.Current.MainWindow).message.Content += "\n" + str;
                            });
                            Broadcast(str, client);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(IPToAddress(client) + " exited with error: " + e.Message);
                }
                finally
                {
                    string username = GetUsernameBySocket(client);
                    if (username != null)
                    {
                        string disconnectMessage = $"USER_DISCONNECTED:{username}";
                        Broadcast(disconnectMessage, client);
                    }

                    userList.Remove(client);
                    client.Close();
                }
            }

            private string GetUsernameBySocket(Socket socket)
            {
                foreach (Socket client in userList)
                {
                    if (client == socket)
                    {
                        // Assuming you have some way to map sockets to usernames
                        // For example, if you maintain a dictionary mapping socket to username
                        // Here, you might need to implement a mapping mechanism or adjust as per your design
                    }
                }
                return null;
            }
            private string IPToAddress(Socket socket)
            {
                // 通过 socket.RemoteEndPoint 获取远程 IP 地址
                IPEndPoint endPoint = socket.RemoteEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "Unknown";
            }
            private void Broadcast(string message, Socket sender)
            {
                foreach (Socket client in userList)
                {
                    if (client != sender)
                    {
                        try
                        {
                            // 发送消息到所有其他客户端
                            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                            client.Send(messageBytes);
                        }
                        catch (Exception e)
                        {
                            // 处理发送失败的情况
                            Console.WriteLine("Error broadcasting message: " + e.Message);
                            // 从用户列表中移除无法发送的客户端
                            userList.Remove(client);
                            client.Close();
                        }
                    }
                }
            }


        }

    }
   }

