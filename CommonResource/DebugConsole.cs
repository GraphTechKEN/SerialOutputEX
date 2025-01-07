using System;
using System.IO;
using System.Runtime.InteropServices;

public class DebugConsole
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    public DebugConsole()
    {

    }

    public void Open()
    {
        /* �Q�l�y�[�W�FC#(Windows Form�A�v���P�[�V����)�ŃR���\�[���̕\���A��\���A�o�͕��@(Console.WriteLine())
         * https://github.com/murasuke/AllocConsoleCSharp
         */

        // Console�\��
        AllocConsole();
        // �R���\�[����stdout�̕R�Â����s���B�����Ă�����͏o�͂ł��邪�A�\���A��\�����J��Ԃ��ƃG���[�ɂȂ�B
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        //�R���\�[���̕����G���R�[�h���w��B���ꂪ�Ȃ���BVE�{�̂���̏�񂪕�����������B
        Console.OutputEncoding = System.Text.Encoding.GetEncoding("utf-8");

    }

    public void Close()
    {
        FreeConsole();
    }
}