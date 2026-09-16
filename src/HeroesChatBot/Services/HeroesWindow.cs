using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeroesChatBot.Services;

public sealed class HeroesWindow
{
    private const string ExpectedWindowClass = "Heroes III";
    private const string ExpectedProcessName = "h3hota HD";
    private const int RestoreWindow = 9;
    private const uint KeyboardInput = 1;
    private const uint KeyUp = 0x0002;
    private const ushort AltKey = 0x12;
    private const ushort BackspaceKey = 0x08;
    private const ushort ControlKey = 0x11;
    private const ushort ShiftKey = 0x10;
    private const uint LeftMouseDown = 0x0002;
    private const uint LeftMouseUp = 0x0004;

    public IntPtr FindLobbyWindow()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!process.ProcessName.Equals(ExpectedProcessName, StringComparison.OrdinalIgnoreCase) ||
                    process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                var className = new char[64];
                var length = GetClassName(process.MainWindowHandle, className, className.Length);
                if (length > 0 &&
                    new string(className, 0, length).Equals(
                        ExpectedWindowClass,
                        StringComparison.Ordinal))
                {
                    return process.MainWindowHandle;
                }
            }
        }

        return IntPtr.Zero;
    }

    public async Task SendToCurrentPrivateRoomAsync(string message, CancellationToken cancellationToken)
    {
        var window = FindLobbyWindow();
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Heroes III is not running. Start the game and enter the online lobby.");
        }

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryActivateWindow(window))
        {
            throw new InvalidOperationException("Could not activate the Heroes III window.");
        }

        await Task.Delay(350, cancellationToken);
        ClickChatInput(window);
        await Task.Delay(150, cancellationToken);
        await SendTextAsync(window, message, cancellationToken);
        try
        {
            await Task.Delay(150, cancellationToken);
            ClickSendButton(window);
            await Task.Delay(200, CancellationToken.None);
        }
        finally
        {
            ClickChatInput(window);
            await Task.Delay(100, CancellationToken.None);
            await ClearUnsentTextAsync(message.Length, CancellationToken.None);
        }
    }

    private static void SendKey(ushort virtualKey)
    {
        SendKeyboardInputs(
            CreateKeyboardInput(virtualKey, 0),
            CreateKeyboardInput(virtualKey, KeyUp));
    }

    private static async Task SendTextAsync(
        IntPtr window,
        string text,
        CancellationToken cancellationToken)
    {
        var threadId = GetWindowThreadProcessId(window, IntPtr.Zero);
        var keyboardLayout = GetKeyboardLayout(threadId);

        foreach (var character in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapping = VkKeyScanEx(character, keyboardLayout);
            if (mapping == -1)
            {
                throw new InvalidOperationException(
                    $"The character '{character}' is unavailable in the keyboard layout currently active in Heroes III.");
            }

            var virtualKey = (ushort)(mapping & 0xFF);
            var modifiers = (mapping >> 8) & 0xFF;
            var inputs = new List<Input>(8);

            AddModifier(inputs, modifiers, 1, ShiftKey, keyUp: false);
            AddModifier(inputs, modifiers, 2, ControlKey, keyUp: false);
            AddModifier(inputs, modifiers, 4, AltKey, keyUp: false);
            inputs.Add(CreateKeyboardInput(virtualKey, 0));
            inputs.Add(CreateKeyboardInput(virtualKey, KeyUp));
            AddModifier(inputs, modifiers, 4, AltKey, keyUp: true);
            AddModifier(inputs, modifiers, 2, ControlKey, keyUp: true);
            AddModifier(inputs, modifiers, 1, ShiftKey, keyUp: true);

            SendKeyboardInputs(inputs.ToArray());
            await Task.Delay(12, cancellationToken);
        }
    }

    private static void AddModifier(
        List<Input> inputs,
        int modifiers,
        int mask,
        ushort virtualKey,
        bool keyUp)
    {
        if ((modifiers & mask) != 0)
        {
            inputs.Add(CreateKeyboardInput(virtualKey, keyUp ? KeyUp : 0));
        }
    }

    private static async Task ClearUnsentTextAsync(
        int characterCount,
        CancellationToken cancellationToken)
    {
        const int keysPerBatch = 32;
        for (var remaining = characterCount; remaining > 0; remaining -= keysPerBatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentBatchSize = Math.Min(remaining, keysPerBatch);
            var inputs = new Input[currentBatchSize * 2];
            for (var index = 0; index < inputs.Length; index += 2)
            {
                inputs[index] = CreateKeyboardInput(BackspaceKey, 0);
                inputs[index + 1] = CreateKeyboardInput(BackspaceKey, KeyUp);
            }

            SendKeyboardInputs(inputs);
            await Task.Delay(8, cancellationToken);
        }
    }

    private static void SendKeyboardInputs(params Input[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Windows could not deliver keyboard input to Heroes III.");
        }
    }

    private static Input CreateKeyboardInput(ushort virtualKey, uint flags) => new()
    {
        Type = KeyboardInput,
        Keyboard = new KeyboardInputData
        {
            VirtualKey = virtualKey,
            Flags = flags
        }
    };

    private static bool TryActivateWindow(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindowAsync(window, RestoreWindow);
        }
        SendKey(AltKey);
        SendKey(AltKey);
        return SetForegroundWindow(window);
    }

    private static void ClickChatInput(IntPtr window)
    {
        ClickLobbyPoint(window, 0.70, 0.963, "message field");
    }

    private static void ClickSendButton(IntPtr window)
    {
        ClickLobbyPoint(window, 0.968, 0.963, "send button");
    }

    private static void ClickLobbyPoint(
        IntPtr window,
        double horizontalRatio,
        double verticalRatio,
        string targetName)
    {
        if (!GetWindowRect(window, out var bounds))
        {
            throw new InvalidOperationException("Could not locate the Heroes III window.");
        }

        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        var targetX = bounds.Left + (int)(width * horizontalRatio);
        var targetY = bounds.Top + (int)(height * verticalRatio);

        if (!SetCursorPos(targetX, targetY))
        {
            throw new InvalidOperationException($"Could not move the mouse to the lobby {targetName}.");
        }

        MouseEvent(LeftMouseDown, 0, 0, 0, UIntPtr.Zero);
        MouseEvent(LeftMouseUp, 0, 0, 0, UIntPtr.Zero);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, char[] className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanEx(char character, IntPtr keyboardLayout);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out WindowBounds bounds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(
        uint flags,
        uint x,
        uint y,
        uint data,
        UIntPtr extraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowBounds
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
