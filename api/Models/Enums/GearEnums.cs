using System.ComponentModel;
using System.Text.Json.Serialization;
using NpgsqlTypes;

namespace api.Models.Enums;

// 대분류
[JsonConverter(typeof(PgNameEnumJsonConverter<GearCategory>))]
public enum GearCategory
{
    [PgName("instrument")]
    Instrument,
    [PgName("audio")]
    Audio,
    [PgName("accessory")]
    Accessory,
    [PgName("etc")]
    Etc
}

// 중분류
// 중분류
[TypeConverter(typeof(GearSubCategoryTypeConverter))]
[JsonConverter(typeof(PgNameEnumJsonConverter<GearSubCategory>))]
public enum GearSubCategory
{
    // Instrument
    [PgName("guitar")]
    Guitar,
    [PgName("bass")]
    Bass,
    [PgName("drum")]
    Drum,
    [PgName("keyboard")]
    Keyboard,
    [PgName("wind")]
    Wind,
    [PgName("string_instrument")]
    StringInstrument,

    // Audio
    [PgName("effects")]
    Effects,
    [PgName("mixer")]
    Mixer,
    [PgName("amp")]
    Amp,
    [PgName("speaker")]
    Speaker,
    [PgName("monitor")]
    Monitor,
    [PgName("audio_interface")]
    AudioInterface,
    [PgName("microphone")]
    Microphone,
    [PgName("headphone")]
    Headphone,
    [PgName("iem")]
    Iem,
    [PgName("earphone")]
    Earphone,

    // Accessory
    [PgName("cable")]
    Cable,
    [PgName("stand")]
    Stand,
    [PgName("case")]
    Case,
    [PgName("pick")]
    Pick,
    [PgName("string_accessory")]
    StringAccessory,
    [PgName("drumstick")]
    Drumstick,

    // Etc
    [PgName("other")]
    Other
}

// 소분류
[TypeConverter(typeof(GearDetailCategoryTypeConverter))]
[JsonConverter(typeof(PgNameEnumJsonConverter<GearDetailCategory>))]
public enum GearDetailCategory
{
    // Guitar
    [PgName("electric")]
    Electric,
    [PgName("acoustic")]
    Acoustic,
    [PgName("classical")]
    Classical,

    // Bass
    [PgName("electric_bass")]
    ElectricBass,
    [PgName("acoustic_bass")]
    AcousticBass,
    [PgName("upright_bass")]
    UprightBass,

    // Drum
    [PgName("acoustic_drum")]
    AcousticDrum,
    [PgName("electronic_drum")]
    ElectronicDrum,
    [PgName("percussion")]
    Percussion,

    // Keyboard
    [PgName("piano")]
    Piano,
    [PgName("synthesizer")]
    Synthesizer,
    [PgName("midi")]
    Midi,
    [PgName("organ")]
    Organ,

    // Wind
    [PgName("saxophone")]
    Saxophone,
    [PgName("trumpet")]
    Trumpet,
    [PgName("flute")]
    Flute,
    [PgName("clarinet")]
    Clarinet,

    // String
    [PgName("violin")]
    Violin,
    [PgName("viola")]
    Viola,
    [PgName("cello")]
    Cello,

    // Effects
    [PgName("multi_effects")]
    MultiEffects,
    [PgName("pedal")]
    Pedal,
    [PgName("bass_effects")]
    BassEffects,
    [PgName("acoustic_effects")]
    AcousticEffects,
    [PgName("pedalboard")]
    Pedalboard,
    [PgName("power_supply")]
    PowerSupply,
    [PgName("effects_other")]
    EffectsOther,

    // Mixer
    [PgName("mixer")]
    Mixer,

    // Amp
    [PgName("amp")]
    Amp,
    [PgName("preamp")]
    Preamp,
    [PgName("power_amp")]
    PowerAmp,

    // Speaker
    [PgName("pa_speaker")]
    PaSpeaker,
    [PgName("subwoofer")]
    Subwoofer,
    [PgName("speaker_system")]
    SpeakerSystem,

    // Monitor
    [PgName("monitor")]
    Monitor,
    [PgName("studio_monitor")]
    StudioMonitor,

    // Audio Interface
    [PgName("usb_interface")]
    UsbInterface,
    [PgName("thunderbolt_interface")]
    ThunderboltInterface,
    [PgName("pcie_interface")]
    PcieInterface,

    // Microphone
    [PgName("condenser")]
    Condenser,
    [PgName("dynamic")]
    Dynamic,
    [PgName("ribbon")]
    Ribbon,
    [PgName("wireless_mic")]
    WirelessMic,

    // Headphone/IEM/Earphone
    [PgName("headphone")]
    Headphone,
    [PgName("headset")]
    Headset,
    [PgName("iem")]
    Iem,
    [PgName("earphone")]
    Earphone,

    // Cable
    [PgName("instrument_cable")]
    InstrumentCable,
    [PgName("mic_cable")]
    MicCable,
    [PgName("speaker_cable")]
    SpeakerCable,
    [PgName("patch_cable")]
    PatchCable,

    // Accessory
    [PgName("stand")]
    Stand,
    [PgName("case")]
    Case,
    [PgName("pick")]
    Pick,
    [PgName("string")]
    @String,
    [PgName("drumstick")]
    Drumstick,

    // Etc
    [PgName("other")]
    Other
}

// 장비 상태
[JsonConverter(typeof(PgNameEnumJsonConverter<GearCondition>))]
[TypeConverter(typeof(GearConditionTypeConverter))]
public enum GearCondition
{
    [PgName("new")]
    New,
    [PgName("like_new")]
    LikeNew,
    [PgName("good")]
    Good,
    [PgName("fair")]
    Fair
}

// 판매 상태
[JsonConverter(typeof(PgNameEnumJsonConverter<GearStatus>))]
public enum GearStatus
{
    [PgName("selling")]
    Selling,
    [PgName("reserved")]
    Reserved,
    [PgName("sold")]
    Sold
}

// 거래 방식
[JsonConverter(typeof(PgNameEnumJsonConverter<TradeMethod>))]
public enum TradeMethod
{
    [PgName("direct")]
    Direct, // 직거래
    [PgName("delivery")]
    Delivery, // 택배
    [PgName("both")]
    Both // 직거래 + 택배
}

// 지역
[JsonConverter(typeof(PgNameEnumJsonConverter<Region>))]
[TypeConverter(typeof(RegionTypeConverter))]
public enum Region
{
    [PgName("서울")]
    Seoul,
    [PgName("부산")]
    Busan,
    [PgName("대구")]
    Daegu,
    [PgName("인천")]
    Incheon,
    [PgName("광주")]
    Gwangju,
    [PgName("대전")]
    Daejeon,
    [PgName("울산")]
    Ulsan,
    [PgName("세종")]
    Sejong,
    [PgName("경기")]
    Gyeonggi,
    [PgName("강원")]
    Gangwon,
    [PgName("충북")]
    Chungbuk,
    [PgName("충남")]
    Chungnam,
    [PgName("전북")]
    Jeonbuk,
    [PgName("전남")]
    Jeonnam,
    [PgName("경북")]
    Gyeongbuk,
    [PgName("경남")]
    Gyeongnam,
    [PgName("제주")]
    Jeju
}
