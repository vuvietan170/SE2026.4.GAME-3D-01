using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Disposable native Game View capture; installed only in the D: validation copy.
public sealed class FloorGuiReadback : MonoBehaviour
{
    public bool Done { get; private set; }
    public string Error { get; private set; }
    public void Capture(string path, int width, int height) { StartCoroutine(Read(path, width, height)); }
    private IEnumerator Read(string path, int width, int height)
    {
        yield return new WaitForEndOfFrame();
        Texture2D image = null;
        try
        {
            image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == null || image.width != width || image.height != height) throw new InvalidOperationException("Unexpected native screenshot size.");
            var pixels = image.GetPixels32(); int light = 0;
            for (int i = 0; i < pixels.Length; i += 97) if (pixels[i].r > 40 || pixels[i].g > 40 || pixels[i].b > 40) light++;
            if (light < 100) throw new InvalidOperationException("Blank native screenshot.");
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        catch (Exception e) { Error = e.ToString(); }
        finally { if (image != null) Destroy(image); Done = true; }
    }
}
