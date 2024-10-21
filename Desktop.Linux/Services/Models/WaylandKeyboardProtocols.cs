using WaylandSharp;

namespace Remotely.Desktop.Linux.Services.Models;

internal sealed class WaylandKeyboardProtocols : WaylandProtocols
{
    private TypeMode? _currentMode;
    private ZwpVirtualKeyboardV1? _keyboard;
    private ZwpVirtualKeyboardManagerV1? _keyboardManager;
    private WlKeyboard? _wlKeyboard;
    private WlSeat? _wlSeat;

    public event EventHandler<WlKeyboard.KeymapEventArgs>? KeymapChanged;

    public override bool Bind(WlRegistry.GlobalEventArgs args, WlRegistry registry)
    {
        if (args.Interface == WlInterface.ZwpVirtualKeyboardManagerV1.Name)
        {
            _keyboardManager = registry.Bind<ZwpVirtualKeyboardManagerV1>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);

            return true;
        }

        if (args.Interface == WlInterface.WlSeat.Name)
        {
            _wlSeat = registry.Bind<WlSeat>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);


            return true;
        }

        return false;
    }

    public void InitializeKeyboard()
    {
        if (_wlSeat is null)
        {
            throw new ApplicationException("WlSeat is null");
        }

        _wlKeyboard = _wlSeat.GetKeyboard();
        _wlKeyboard.Keymap += (sender, eventArgs) =>
        {
            if (_keyboard is not null)
            {
                return;
            }

            CreateVirtualKeyboard(eventArgs);
            KeymapChanged?.Invoke(sender, eventArgs);
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _keyboardManager?.Dispose();
        _keyboard?.Dispose();
        _wlSeat?.Dispose();
        _wlKeyboard?.Dispose();
    }

    public uint SendKey(string key, bool pressed)
    {
        if (_keyboard is null)
        {
            throw new ApplicationException("Keyboard is null");
        }

        uint mappedKey = ConvertJavaScriptKeyToWaylandKeyCode(key);

        if (TryGetModifiers(key, out TypeMode modifiers))
        {
            switch (pressed)
            {
                case true when _currentMode != TypeMode.Capslock:
                    _currentMode = modifiers;
                    break;
                case true when _currentMode == TypeMode.Capslock:
                    _currentMode = TypeMode.None;
                    break;
                default:
                {
                    if (_currentMode != TypeMode.Capslock)
                    {
                        _currentMode = TypeMode.None;
                    }

                    break;
                }
            }

            _keyboard.Modifiers((uint)_currentMode, 0, (uint)_currentMode, 0);

            return mappedKey;
        }

        _keyboard!.Key(Time, mappedKey, (uint)(pressed ? 1 : 0));

        return mappedKey;
    }

    private void CreateVirtualKeyboard(WlKeyboard.KeymapEventArgs keymapEventArgs)
    {
        _keyboard = _keyboardManager!.CreateVirtualKeyboard(_wlSeat!);
        _keyboard.Keymap((uint)keymapEventArgs.Format, keymapEventArgs.Fd, keymapEventArgs.Size);
    }

    private static uint ConvertJavaScriptKeyToWaylandKeyCode(string key)
    {
        uint keyCode = key switch
        {
            #region Navigation keys

            "Home" => 102, // KEY_HOME
            "End" => 107, // KEY_END

            #endregion

            #region Arrow keys

            "ArrowDown" => 108, // KEY_DOWN
            "ArrowUp" => 103, // KEY_UP
            "ArrowLeft" => 105, // KEY_LEFT
            "ArrowRight" => 106, // KEY_RIGHT

            #endregion

            #region Special keys

            "Enter" => 28, // KEY_ENTER
            "Esc" => 1, // KEY_ESC
            "Alt" => 56, // KEY_LEFTALT
            "Control" => 29, // KEY_LEFTCTRL
            "Shift" => 42, // KEY_LEFTSHIFT
            "Backspace" => 14, // KEY_BACKSPACE
            "Tab" => 15, // KEY_TAB
            "CapsLock" => 58, // KEY_CAPSLOCK
            "Delete" => 111, // KEY_DELETE
            "PageUp" => 104, // KEY_PAGEUP
            "PageDown" => 109, // KEY_PAGEDOWN
            "NumLock" => 69, // KEY_NUMLOCK
            "ScrollLock" => 70, // KEY_SCROLLLOCK
            "ContextMenu" => 127, // KEY_COMPOSE (Context menu key)

            #endregion

            #region Punctuation and symbols

            " " => 57, // KEY_SPACE
            "!" => 2, // Shift + KEY_1 (KEY_1 = 2)
            "\"" => 40, // Shift + KEY_APOSTROPHE (KEY_APOSTROPHE = 40)
            "#" => 4, // Shift + KEY_3 (KEY_3 = 4)
            "$" => 5, // Shift + KEY_4 (KEY_4 = 5)
            "%" => 6, // Shift + KEY_5 (KEY_5 = 6)
            "&" => 8, // Shift + KEY_7 (KEY_7 = 8)
            "'" => 40, // KEY_APOSTROPHE
            "(" => 10, // Shift + KEY_9 (KEY_9 = 10)
            ")" => 11, // Shift + KEY_0 (KEY_0 = 11)
            "*" => 9, // Shift + KEY_8 (KEY_8 = 9)
            "+" => 13, // Shift + KEY_EQUAL (KEY_EQUAL = 13)
            "," => 51, // KEY_COMMA
            "-" => 12, // KEY_MINUS
            "." => 52, // KEY_DOT
            "/" => 53, // KEY_SLASH
            ":" => 39, // Shift + KEY_SEMICOLON (KEY_SEMICOLON = 39)
            ";" => 39, // KEY_SEMICOLON
            "<" => 51, // Shift + KEY_COMMA (KEY_COMMA = 51)
            "=" => 13, // KEY_EQUAL
            ">" => 52, // Shift + KEY_DOT (KEY_DOT = 52)
            "?" => 53, // Shift + KEY_SLASH (KEY_SLASH = 53)
            "@" => 3, // Shift + KEY_2 (KEY_2 = 3)
            "[" => 26, // KEY_LEFTBRACE
            "\\" => 43, // KEY_BACKSLASH
            "]" => 27, // KEY_RIGHTBRACE
            "^" => 7, // Shift + KEY_6 (KEY_6 = 7)
            "_" => 12, // Shift + KEY_MINUS (KEY_MINUS = 12)
            "`" => 41, // KEY_GRAVE
            "{" => 26, // Shift + KEY_LEFTBRACE (KEY_LEFTBRACE = 26)
            "|" => 43, // Shift + KEY_BACKSLASH (KEY_BACKSLASH = 43)
            "}" => 27, // Shift + KEY_RIGHTBRACE (KEY_RIGHTBRACE = 27)
            "~" => 41, // Shift + KEY_GRAVE (KEY_GRAVE = 41)

            #endregion

            #region Numbers

            "0" => 11, // KEY_0
            "1" => 2, // KEY_1
            "2" => 3, // KEY_2
            "3" => 4, // KEY_3
            "4" => 5, // KEY_4
            "5" => 6, // KEY_5
            "6" => 7, // KEY_6
            "7" => 8, // KEY_7
            "8" => 9, // KEY_8
            "9" => 10, // KEY_9

            #endregion

            #region Alphabetical keys (lowercase)

            "a" => 30, "b" => 48, "c" => 46, "d" => 32, "e" => 18,
            "f" => 33, "g" => 34, "h" => 35, "i" => 23, "j" => 36,
            "k" => 37, "l" => 38, "m" => 50, "n" => 49, "o" => 24,
            "p" => 25, "q" => 16, "r" => 19, "s" => 31, "t" => 20,
            "u" => 22, "v" => 47, "w" => 17, "x" => 45, "y" => 21,
            "z" => 44,

            #endregion

            #region Alphabetical keys (uppercase)

            "A" => 30, "B" => 48, "C" => 46, "D" => 32, "E" => 18,
            "F" => 33, "G" => 34, "H" => 35, "I" => 23, "J" => 36,
            "K" => 37, "L" => 38, "M" => 50, "N" => 49, "O" => 24,
            "P" => 25, "Q" => 16, "R" => 19, "S" => 31, "T" => 20,
            "U" => 22, "V" => 47, "W" => 17, "X" => 45, "Y" => 21,
            "Z" => 44,

            #endregion

            // Default case for unmapped keys
            _ => 0 // Unknown key
        };

        return keyCode;
    }

    private static bool TryGetModifiers(string key, out TypeMode mods)
    {
        mods = key switch
        {
            "Alt" => TypeMode.Alt,
            "Control" => TypeMode.Ctrl,
            "Shift" => TypeMode.Shift,
            "CapsLock" => TypeMode.Capslock,

            _ => TypeMode.None
        };

        return mods != TypeMode.None;
    }
}

internal enum TypeMode : uint
{
    None = 0,
    Shift = 1,
    Capslock = 2,
    Ctrl = 4,
    Alt = 8,
    Logo = 64,
    AltGr = 128
}