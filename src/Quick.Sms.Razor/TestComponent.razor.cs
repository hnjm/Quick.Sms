﻿using Microsoft.AspNetCore.Components;
using Quick.Blazor.Bootstrap.Admin.Utils;
using Quick.Blazor.Bootstrap;

namespace Quick.Sms.Razor
{
    public partial class TestComponent : ComponentBase, IDisposable
    {
        private ModalLoading modalLoading;
        private ModalPrompt modalPrompt;
        private ModalAlert modalAlert;
        private Quick.Blazor.Bootstrap.Admin.LogViewControl logViewControl;

        private bool isOpen = false;
        private int baudRate = 115200;
        private string deviceType = "";
        private string portName;

        private ISmsDevice device;
        private string sendTo;
        private string sendContent = "{device}({portName},{baudRate}),{time}";
        private CommandType commandType;
        private Dictionary<SmsDeviceStatus, string> statusDict;
        private string commandText;

        private async void BtnOpen_Click()
        {
            if (string.IsNullOrEmpty(portName))
            {
                modalAlert.Show("错误", $"请先选择串口!");
                return;
            }
            if (string.IsNullOrEmpty(deviceType))
            {
                modalAlert.Show("错误", $"请先选择类型!");
                return;
            }

            try
            {
                modalLoading.Show("打开串口", $"正在打开串口[{portName}]...", true);
                await OpenSerialPort(deviceType, portName, baudRate);
            }
            catch (Exception ex)
            {
                CloseSerialPort();
                modalAlert.Show("错误", $"串口[{portName}]打开失败，原因：{ex.Message}");
            }
            finally
            {
                modalLoading.Close();
            }
        }

        private async void BtnScan_Click()
        {
            try
            {
                if (string.IsNullOrEmpty(portName))
                {
                    modalAlert.Show("错误", $"请先选择串口!");
                    return;
                }
                modalLoading.Show("智能识别中...", null, true);
                var deviceTypeInfo = await Task.Run(() => AbstractSerialPortModem.Scan(portName, baudRate));
                deviceType = deviceTypeInfo.Id;
                modalAlert.Show("成功", $"已成功识别为[{deviceTypeInfo.Name}]!");
            }
            catch (Exception ex)
            {
                deviceType = null;
                modalAlert.Show("失败", $"识别失败!{Environment.NewLine}{ex.Message}");
            }
            finally
            {
                modalLoading.Close();
                await InvokeAsync(StateHasChanged);
            }
        }

        private void pushLog(string log)
        {
            logViewControl?.AddLine($"{DateTime.Now.ToString("HH:mm:ss.ffff")} {log}");
        }

        private async Task OpenSerialPort(string deviceTypeId, string portName, int baudRate)
        {
            device = SmsDeviceManager.Instnce.CreateDeviceInstance(deviceTypeId,
                new SerialPortModemSetting()
                {
                    PortName = portName,
                    BaudRate = baudRate
                });
            device.LineSended += (sender, line) => pushLog("TX " + line);
            device.LineRecved += (sender, line) => pushLog("RX " + line);

            await Task.Run(() => device.Open());
            statusDict = device.Status.ToDictionary(t => t, t => String.Empty);
            isOpen = true;
            StateHasChanged();
        }

        private void CloseSerialPort()
        {
            try { device?.Close(); } catch { }
            isOpen = false;
            logViewControl?.Clear();
        }

        private async Task btnSend_Click()
        {
            if (string.IsNullOrEmpty(sendTo))
            {
                modalAlert.Show("错误", $"请输入要发送到的号码!");
                return;
            }
            if (string.IsNullOrEmpty(sendContent))
            {
                modalAlert.Show("错误", $"请输入短信内容!");
                return;
            }
            var content = sendContent;
            content = content.Replace("{portName}", portName);
            content = content.Replace("{baudRate}", baudRate.ToString());
            content = content.Replace("{device}", device.Name);
            content = content.Replace("{time}", DateTime.Now.ToString());
            content = content.Replace("{guid}", Guid.NewGuid().ToString("N"));

            try
            {
                modalLoading.Show("发送短信中", $"正在向[{sendTo}]发送短信...", true);
                await Task.Run(() => device.Send(sendTo, content));
                modalAlert.Show("成功", $"发送短信成功。");
            }
            catch (Exception ex)
            {
                modalAlert.Show("失败", $"发送短信失败，原因：{ExceptionUtils.GetExceptionMessage(ex)}");
            }
            finally
            {
                modalLoading.Close();
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ReadStatus(SmsDeviceStatus status, bool shouldCloseLoading = true)
        {
            try
            {
                modalLoading.Show("读取中", $"正在读取[{status.Name}]的数据...", true);
                string ret = null;
                await Task.Run(() => ret = status.Read());
                statusDict[status] = ret;
            }
            catch (Exception ex)
            {
                statusDict[status] = $"读取失败，原因：{ExceptionUtils.GetExceptionMessage(ex)}";
            }
            finally
            {
                if (shouldCloseLoading)
                    modalLoading.Close();
                await InvokeAsync(StateHasChanged);
            }
        }

        private void WriteStatus(SmsDeviceStatus status)
        {
            modalPrompt.Show($"请输入要设置[{status.Name}]的值", statusDict[status], async content =>
            {
                try
                {
                    modalLoading.Show("写入中", $"正在写入[{status.Name}]的数据...", true);
                    await Task.Run(() => status.Write(content));
                    statusDict[status] = content;
                    modalAlert.Show("成功", $"写入[{status.Name}]的值成功。");
                }
                catch (Exception ex)
                {
                    modalAlert.Show("失败", $"写入[{status.Name}]的值失败，原因：{ExceptionUtils.GetExceptionMessage(ex)}");
                }
                finally
                {
                    modalLoading.Close();
                    await InvokeAsync(StateHasChanged);
                }
            });
        }

        private async Task ReadAllStatus()
        {
            foreach (var status in statusDict.Keys)
                await ReadStatus(status, false);
            modalLoading.Close();
        }

        private void BtnSendAT_Click()
        {
            if (string.IsNullOrEmpty(commandText))
            {
                modalAlert.Show("错误", $"请输入指令内容!");
                return;
            }
            switch (commandType)
            {
                case CommandType.Text:
                    device.ExecuteCommand(commandText);
                    break;
                case CommandType.Hex:
                    device.ExecuteCommand(commandText.ToHex());
                    break;
            }
        }

        public void Dispose()
        {
            CloseSerialPort();
        }
    }
}
