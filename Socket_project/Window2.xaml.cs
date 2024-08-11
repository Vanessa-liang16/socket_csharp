using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Intrinsics.X86;
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
    /// <summary>
    /// Window2.xaml 的互動邏輯
    /// </summary>
    
    public partial class Window2 : Window
    {
        private Socket _clientSocket;
        private Thread _receiveThread;

        public Window2()
        {
            InitializeComponent();
            
        }
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ipAddress = IP2.Text;
                int port = int.Parse(port2.Text);

                IPAddress ip = IPAddress.Parse(ipAddress);
                IPEndPoint endPoint = new IPEndPoint(ip, port);

                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.Connect(endPoint);

                string username = user2.Text;
                string connectMessage = $"user connect:{username}";
                byte[] connectMessageBytes = Encoding.UTF8.GetBytes(connectMessage);
                _clientSocket.Send(connectMessageBytes);

                client_message2.Content = "Connected successfully";

                // Start receiving messages
                _receiveThread = new Thread(ReceiveMessages);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                client_message2.Content = $"Connection failed: {ex.Message}";
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
                            client_message2.Content += $"\n{receivedMessage}";
                        });
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        client_message2.Content += $"\nError receiving message: {ex.Message}";
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
                    string message = message2.Text;
                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    _clientSocket.Send(messageBytes);

                    // Also add the message to the local message box
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        client_message2.Content += $"\nYou: {message}";
                    });

                    // Clear the message box
                    message2.Text = string.Empty;
                }
                else
                {
                    client_message2.Content += "Not connected";
                }
            }
            catch (Exception ex)
            {
                client_message2.Content = $"Sending message failed: {ex.Message}";
            }
        }
        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clientSocket != null && _clientSocket.Connected)
                {
                    string username = user2.Text;
                    string disconnectMessage = $"user disconnect:{username}";
                    byte[] disconnectMessageBytes = Encoding.UTF8.GetBytes(disconnectMessage);
                    _clientSocket.Send(disconnectMessageBytes);

                    _clientSocket.Shutdown(SocketShutdown.Both);
                    _clientSocket.Close();
                    client_message2.Content = "Disconnected successfully";
                }
                else
                {
                    client_message2.Content = "Not connected";
                }
            }
            catch (Exception ex)
            {
                client_message2.Content = "";
            }
        }
    }
    
}
