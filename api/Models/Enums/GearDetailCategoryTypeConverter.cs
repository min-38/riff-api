using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace api.Models.Enums;

public class GearDetailCategoryTypeConverter : TypeConverter
{
    private static readonly Dictionary<string, GearDetailCategory> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        { "electric", GearDetailCategory.Electric },
        { "acoustic", GearDetailCategory.Acoustic },
        { "classical", GearDetailCategory.Classical },
        { "electric_bass", GearDetailCategory.ElectricBass },
        { "acoustic_bass", GearDetailCategory.AcousticBass },
        { "upright_bass", GearDetailCategory.UprightBass },
        { "acoustic_drum", GearDetailCategory.AcousticDrum },
        { "electronic_drum", GearDetailCategory.ElectronicDrum },
        { "percussion", GearDetailCategory.Percussion },
        { "piano", GearDetailCategory.Piano },
        { "synthesizer", GearDetailCategory.Synthesizer },
        { "midi", GearDetailCategory.Midi },
        { "organ", GearDetailCategory.Organ },
        { "saxophone", GearDetailCategory.Saxophone },
        { "trumpet", GearDetailCategory.Trumpet },
        { "flute", GearDetailCategory.Flute },
        { "clarinet", GearDetailCategory.Clarinet },
        { "violin", GearDetailCategory.Violin },
        { "viola", GearDetailCategory.Viola },
        { "cello", GearDetailCategory.Cello },
        { "multi_effects", GearDetailCategory.MultiEffects },
        { "pedal", GearDetailCategory.Pedal },
        { "bass_effects", GearDetailCategory.BassEffects },
        { "acoustic_effects", GearDetailCategory.AcousticEffects },
        { "pedalboard", GearDetailCategory.Pedalboard },
        { "power_supply", GearDetailCategory.PowerSupply },
        { "effects_other", GearDetailCategory.EffectsOther },
        { "mixer", GearDetailCategory.Mixer },
        { "amp", GearDetailCategory.Amp },
        { "preamp", GearDetailCategory.Preamp },
        { "power_amp", GearDetailCategory.PowerAmp },
        { "pa_speaker", GearDetailCategory.PaSpeaker },
        { "subwoofer", GearDetailCategory.Subwoofer },
        { "speaker_system", GearDetailCategory.SpeakerSystem },
        { "monitor", GearDetailCategory.Monitor },
        { "studio_monitor", GearDetailCategory.StudioMonitor },
        { "usb_interface", GearDetailCategory.UsbInterface },
        { "thunderbolt_interface", GearDetailCategory.ThunderboltInterface },
        { "pcie_interface", GearDetailCategory.PcieInterface },
        { "condenser", GearDetailCategory.Condenser },
        { "dynamic", GearDetailCategory.Dynamic },
        { "ribbon", GearDetailCategory.Ribbon },
        { "wireless_mic", GearDetailCategory.WirelessMic },
        { "headphone", GearDetailCategory.Headphone },
        { "headset", GearDetailCategory.Headset },
        { "iem", GearDetailCategory.Iem },
        { "earphone", GearDetailCategory.Earphone },
        { "instrument_cable", GearDetailCategory.InstrumentCable },
        { "mic_cable", GearDetailCategory.MicCable },
        { "speaker_cable", GearDetailCategory.SpeakerCable },
        { "patch_cable", GearDetailCategory.PatchCable },
        { "stand", GearDetailCategory.Stand },
        { "case", GearDetailCategory.Case },
        { "pick", GearDetailCategory.Pick },
        { "string", GearDetailCategory.@String },
        { "drumstick", GearDetailCategory.Drumstick },
        { "other", GearDetailCategory.Other }
    };

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            if (Map.TryGetValue(text, out var detailCategory))
                return detailCategory;

            if (Enum.TryParse<GearDetailCategory>(text, ignoreCase: true, out var parsed))
                return parsed;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is GearDetailCategory detailCategory)
        {
            foreach (var pair in Map)
            {
                if (pair.Value == detailCategory)
                    return pair.Key;
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
