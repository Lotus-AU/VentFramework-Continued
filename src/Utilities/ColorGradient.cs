using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;

namespace VentLib.Utilities;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class ColorGradient
{
    private Color[] colors;
    private float spacing;

    public ColorGradient(params Color[] colors)
    {
        this.colors = colors;
        spacing = 1f / (this.colors.Length - 1);
    }

    public Color Evaluate(float percent)
    {
        if (percent > 1) percent = 1;
        int indexLow = Mathf.FloorToInt(percent / spacing);
        if (indexLow >= colors.Length - 1) return colors[^1];
        int indexHigh = indexLow + 1;
        float percentClamp = (colors.Length - 1) * (percent - indexLow * spacing);

        Color colorA = colors[indexLow];
        Color colorB = colors[indexHigh];

        float r = colorA.r + percentClamp * (colorB.r - colorA.r);
        float g = colorA.g + percentClamp * (colorB.g - colorA.g);
        float b = colorA.b + percentClamp * (colorB.b - colorA.b);

        return new Color(r, g, b);
    }

    public string Apply(string input, int degree = 1)
    {
        if (input.Length == 0) return input;

        int visibleLength = 0;
        bool inTag = false;
        foreach (char c in input)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) visibleLength++;
        }

        if (visibleLength == 0) return input;
        if (visibleLength == degree) return colors[0].Colorize(input);

        float step = 1f / (visibleLength - 1);
        StringBuilder builder = new();
        int visibleIndex = 0;
        int i = 0;

        while (i < input.Length)
        {
            if (input[i] == '<')
            {
                int tagEnd = input.IndexOf('>', i);
                if (tagEnd == -1) tagEnd = input.Length - 1; 
                builder.Append(input, i, tagEnd - i + 1);
                i = tagEnd + 1;
                continue;
            }

            int chunkStart = i;
            int count = 0;
            while (count < degree && i < input.Length && input[i] != '<')
            {
                i++;
                count++;
            }

            string chunk = input[chunkStart..i];
            builder.Append(Evaluate(step * visibleIndex).Colorize(chunk));
            visibleIndex += count;
        }

        return builder.ToString();
    }
}