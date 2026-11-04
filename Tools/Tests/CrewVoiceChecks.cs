using System;
using System.Linq;
using System.Threading.Tasks;

public static class CrewVoiceChecks
{
    private static int checks;
    private static void Check(bool condition,string reason)
    {if(!condition)throw new Exception(reason);checks++;}
    private static float[] Tone(float frequency,float amplitude=.45f)
    {return Enumerable.Range(0,CrewVoiceCodec.Samples).Select(i=>(float)Math.Sin(i*2*Math.PI*frequency/CrewVoiceCodec.SampleRate)*amplitude).ToArray();}
    private static float[] Decode(float[] input,ushort sequence=1,int timestamp=100)
    {
        var packet=CrewVoiceCodec.Encode(input,sequence,timestamp);
        Check(packet.Length==490,"Voice packet must stay below one network MTU.");
        var output=new float[input.Length];
        Check(CrewVoiceCodec.Decode(packet,output,out var seq,out var time),"Valid frame rejected.");
        Check(seq==sequence && time==timestamp,"Header failed signed timestamp/sequence roundtrip.");return output;
    }
    public static void Main()
    {
        foreach(int timestamp in new[]{0,-1,int.MinValue,int.MaxValue})Decode(Tone(400),ushort.MaxValue,timestamp);
        foreach(float frequency in new[]{150f,400f,1000f,2200f})
        {
            var original=Tone(frequency);var decoded=Decode(original);
            double signal=0,error=0;
            for(int i=40;i<original.Length;i++){signal+=original[i]*original[i];double d=decoded[i]-original[i];error+=d*d;}
            Check(10*Math.Log10(signal/error)>15,"Voice tone quality is too low at "+frequency+"Hz.");
            Check(decoded.All(value=>!float.IsNaN(value) && Math.Abs(value)<=1),"Decoded audio exceeded valid sample range.");
        }
        var silence=Decode(new float[CrewVoiceCodec.Samples]);
        Check(Math.Sqrt(silence.Sum(v=>(double)v*v)/silence.Length)<.001,"Silence produced audible codec noise.");
        var clipping=Decode(Enumerable.Range(0,CrewVoiceCodec.Samples).Select(i=>i%2==0?10f:-10f).ToArray());
        Check(clipping.All(v=>v>=-1 && v<=1),"Saturated microphone input was not clamped.");
        var invalidSamples=new float[CrewVoiceCodec.Samples];invalidSamples[0]=float.NaN;invalidSamples[1]=float.PositiveInfinity;
        Check(Decode(invalidSamples).All(v=>!float.IsNaN(v) && !float.IsInfinity(v)),"Non-finite samples entered playback.");
        var packet=CrewVoiceCodec.Encode(Tone(400),4,600);var output=new float[CrewVoiceCodec.Samples];
        Check(!CrewVoiceCodec.Decode(null,output,out _,out _),"Null packet accepted.");
        Check(!CrewVoiceCodec.Decode(new byte[packet.Length+1],output,out _,out _),"Wrong-sized packet accepted.");
        Check(!CrewVoiceCodec.Decode(packet,new float[1],out _,out _),"Wrong-sized output accepted.");
        packet[0]=99;Check(!CrewVoiceCodec.Decode(packet,output,out _,out _),"Unknown codec version accepted.");
        packet[0]=1;packet[1]=255;Check(!CrewVoiceCodec.Decode(packet,output,out _,out _),"Invalid ADPCM index accepted.");
        var isolated=Decode(Tone(700),12,1234);Decode(Tone(2000),55,2000);
        Check(isolated.SequenceEqual(Decode(Tone(700),12,1234)),"Packet decoding depended on earlier audio frames.");
        Check(CrewVoiceCodec.Fresh(100,700) && !CrewVoiceCodec.Fresh(100,701),"Stale audio expiry boundary failed.");
        Check(CrewVoiceCodec.Fresh(250,100) && !CrewVoiceCodec.Fresh(251,100),"Future packet guard failed.");
        Check(CrewVoiceCodec.Fresh(int.MaxValue-20,unchecked(int.MaxValue+80)),"Server clock wrap rejected fresh audio.");
        var buffer=new CrewVoiceBuffer();var frame=Enumerable.Repeat(.25f,CrewVoiceCodec.Samples).ToArray();
        Check(buffer.Push(frame,65535),"First voice frame rejected.");
        var read=new float[CrewVoiceCodec.Samples];buffer.Read(read);
        Check(read.All(v=>v==0) && buffer.Queued==CrewVoiceCodec.Samples,"Jitter prefill played too soon or consumed audio.");
        Check(buffer.Push(frame,0),"Sequence wrap rejected.");
        Check(!buffer.Push(frame,0) && !buffer.Push(frame,65535),"Duplicate/reordered packet accepted.");
        buffer.Read(read);Check(read.All(v=>v==.25f),"Primed buffer did not play queued audio.");
        var starved=new float[CrewVoiceCodec.Samples*2];buffer.Read(starved);
        Check(starved[0]==.25f && starved[starved.Length-1]==0 && buffer.Queued==0,"Audio underflow did not fade to silence.");
        buffer.Read(read);Check(read.All(v=>v==0),"Underflow replayed old samples.");
        buffer.Clear();Check(buffer.Queued==0 && !buffer.Push(frame,0),"Mute clearing reset duplicate protection.");
        Check(buffer.Push(frame,20) && buffer.Queued==CrewVoiceCodec.Samples,"Long transmission gap retained stale audio.");
        Check(buffer.Push(frame,22),"Packet following one lost frame rejected.");
        Check(buffer.Queued==CrewVoiceCodec.Samples*3,"Packet loss was not padded to preserve timing.");
        for(ushort i=23;i<100;i++)buffer.Push(frame,i);
        Check(buffer.Queued<=CrewVoiceCodec.Samples*5,"Voice backlog grew beyond its latency bound.");
        var concurrent=new CrewVoiceBuffer();
        var producer=Task.Run(()=>{for(ushort i=0;i<500;i++)concurrent.Push(frame,i);});
        var consumer=Task.Run(()=>{var block=new float[512];for(int i=0;i<700;i++){concurrent.Read(block);if(block.Any(v=>float.IsNaN(v) || Math.Abs(v)>1))throw new Exception("Audio thread data corruption.");}});
        Task.WaitAll(producer,consumer);Check(concurrent.Queued<=CrewVoiceCodec.Samples*5,"Concurrent voice buffer overflow.");
        Console.WriteLine("Crew voice: "+checks+" codec, packet and playback checks passed.");
    }
}
