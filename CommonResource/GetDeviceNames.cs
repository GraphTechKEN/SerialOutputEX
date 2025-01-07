using System.Management;
using System.Text.RegularExpressions;
using System;
using Microsoft.Win32;
using System.Text;


public class GetDeviceNames
{
    public GetDeviceNames(out string[] devices)
    {
        var deviceNameList = new System.Collections.ArrayList();
        Regex regexPortName = new Regex(@"(COM\d+)");

        ManagementClass mcPnPEntity = new ManagementClass("Win32_PnPEntity");
        ManagementObjectCollection manageObjCol = mcPnPEntity.GetInstances();

        //�S�Ă�PnP�f�o�C�X��T�����V���A���ʐM���s����f�o�C�X�𐏎��ǉ�����
        foreach (ManagementObject manageObj in manageObjCol)
        {
            var namePropertyValue = manageObj["Name"];//Name�v���p�e�B���擾
            if (namePropertyValue != null)
            {
                string classGuid = manageObj["ClassGuid"] as string; // GUID
                string devicePass = manageObj["DeviceID"] as string; // �f�o�C�X�C���X�^���X�p�X
                                                                     //Name�v���p�e�B������̈ꕔ��"(COM1)�`(COM999)"�ƈ�v����Ƃ����X�g�ɒǉ�"
                string name = namePropertyValue.ToString();
                if (regexPortName.IsMatch(name) && classGuid != null && devicePass != null)
                {

                    // �f�o�C�X�C���X�^���X�p�X����V���A���ʐM�ڑ��@��݂̂𒊏o
                    // {4d36e978-e325-11ce-bfc1-08002be10318}�̓V���A���ʐM�ڑ��@��������Œ�l
                    if (String.Equals(classGuid, "{4d36e978-e325-11ce-bfc1-08002be10318}",
                            StringComparison.InvariantCulture))
                    {

                        // �f�o�C�X�C���X�^���X�p�X����f�o�C�XID��2�i�K�Ŕ����o��
                        string[] tokens = devicePass.Split('&');

                        //Bluetooth�f�o�C�X�����̑�(USB��)�f�o�C�X���𔻕�
                        //Bluetooth�f�o�C�X�̂Ƃ�
                        if (tokens.Length > 4)
                        {
                            string[] addressToken = tokens[4].Split('_');
                            string[] deviceType = tokens[0].Split('\\');
                            string bluetoothAddress = addressToken[0];
                            if (deviceType[0] == "BTHENUM")
                            {
                                Match m = regexPortName.Match(name);

                                string comPortNumber = "";
                                if (m.Success)
                                {
                                    // COM�ԍ��𔲂��o��
                                    comPortNumber = m.Groups[1].ToString();
                                }

                                if (Convert.ToUInt64(bluetoothAddress, 16) > 0)
                                {
                                    string bluetoothName = GetBluetoothRegistryName(bluetoothAddress);
                                    deviceNameList.Add(bluetoothName + " (" + comPortNumber + ")");
                                }
                            }
                            //����ȊO�̂Ƃ�
                            else
                            {
                                deviceNameList.Add(name);
                            }
                        }
                        //����ȊO�̂Ƃ�
                        else
                        {
                            deviceNameList.Add(name);
                        }
                    }
                }
            }
        }

        //�߂�l�쐬
        if (deviceNameList.Count > 0)
        {
            string[] deviceNames = new string[deviceNameList.Count];
            int index = 0;
            foreach (var name in deviceNameList)
            {
                deviceNames[index++] = name.ToString();
            }
            devices = deviceNames;
        }
        else
        {
            devices = null;
        }
    }

    /// <summary>�@�햼�̎擾</summary> 
    /// <param name="address">[in] �A�h���X</param> 
    /// <returns>[out] �@�햼��</returns> 
    private string GetBluetoothRegistryName(string address)
    {
        string deviceName = "";
        // �ȉ��̃��W�X�g���p�X�͂ǂ�PC�ł�����
        string registryPath = @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices";
        string devicePath = String.Format(@"{0}\{1}", registryPath, address);

        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(devicePath))
        {
            if (key != null)
            {
                Object o = key.GetValue("Name");

                byte[] raw = o as byte[];

                if (raw != null)
                {
                    // ASCII�ϊ�
                    deviceName = Encoding.ASCII.GetString(raw);
                }
            }
        }
        // NULL�������g���~���O���ă��^�[��
        return deviceName.TrimEnd('\0');
    }
}