using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tombol-tombol menu/permainan di satu tempat.
/// Otomatis mendukung Input System baru maupun Input Manager lama.
/// (Tombol kemudi mobil tetap ada di CarController.)
/// </summary>
public static class InputHelper
{
    /// <summary>ENTER atau SPASI: mulai dari menu.</summary>
    public static bool StartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.enterKey.wasPressedThisFrame
                          || k.numpadEnterKey.wasPressedThisFrame
                          || k.spaceKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    /// <summary>ENTER saja (tanpa spasi, karena spasi = rem).</summary>
    public static bool ConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    /// <summary>R: main ulang.</summary>
    public static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    /// <summary>G: tampil/sembunyikan grafik.</summary>
    public static bool GraphTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.gKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.G);
#endif
    }

    /// <summary>O: buka folder berisi file CSV.</summary>
    public static bool OpenFolderPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.oKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.O);
#endif
    }

    /// <summary>ESC: kembali ke menu.</summary>
    public static bool MenuPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}