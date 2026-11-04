using System;

// Independently decodable 60ms mono IMA-ADPCM frames. No Unity calls or allocations
// in Decode, so malformed/late packets cannot alter another speaker's codec state.
public static class CrewVoiceCodec
{
    public const int SampleRate=16000, Samples=960, Header=10, PacketSize=490;
    private static readonly int[] Steps={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
    private static readonly int[] Shifts={-1,-1,-1,-1,2,4,6,8};
    private static int Clamp(int n,int lo,int hi)=>Math.Min(hi,Math.Max(lo,n));
    public static bool Fresh(int timestamp,int now)
    {int age=unchecked(now-timestamp);return age>=-150 && age<=600;}
    private static int Pcm(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:(int)(Math.Max(-1,Math.Min(1,value))*32767);
    private static int Step(int code,ref int predictor,ref int index)
    {
        int step=Steps[index],delta=step>>3;
        if((code&4)!=0)delta+=step;
        if((code&2)!=0)delta+=step>>1;
        if((code&1)!=0)delta+=step>>2;
        predictor=Clamp(predictor+((code&8)!=0?-delta:delta),-32768,32767);
        index=Clamp(index+Shifts[code&7],0,88);return predictor;
    }
    public static byte[] Encode(float[] samples,ushort sequence,int timestamp)
    {
        if(samples==null || samples.Length!=Samples)throw new ArgumentException("Expected one mono voice frame.");
        var packet=new byte[PacketSize];int predictor=Pcm(samples[0]),index=40;
        packet[0]=1;packet[1]=(byte)index;packet[2]=(byte)sequence;packet[3]=(byte)(sequence>>8);
        for(int i=0;i<4;i++)packet[4+i]=(byte)(timestamp>>(i*8));
        packet[8]=(byte)predictor;packet[9]=(byte)(predictor>>8);
        for(int i=1;i<Samples;i++)
        {
            int difference=Pcm(samples[i])-predictor,code=difference<0?8:0,step=Steps[index];
            int remaining=Math.Abs(difference);
            if(remaining>=step){code|=4;remaining-=step;}
            if(remaining>=(step>>1)){code|=2;remaining-=step>>1;}
            if(remaining>=(step>>2))code|=1;
            Step(code,ref predictor,ref index);
            int nibble=i-1;packet[Header+nibble/2]|=(byte)(code<<((nibble&1)*4));
        }
        return packet;
    }
    public static bool Decode(byte[] packet,float[] output,out ushort sequence,out int timestamp)
    {
        sequence=0;timestamp=0;
        if(packet==null || packet.Length!=PacketSize || packet[0]!=1 || packet[1]>88 || output==null || output.Length!=Samples)return false;
        sequence=(ushort)(packet[2]|packet[3]<<8);
        timestamp=packet[4]|packet[5]<<8|packet[6]<<16|packet[7]<<24;
        int predictor=(short)(packet[8]|packet[9]<<8),index=packet[1];output[0]=predictor/32768f;
        for(int i=1;i<Samples;i++)
        {
            int nibble=i-1,code=(packet[Header+nibble/2]>>((nibble&1)*4))&15;
            output[i]=Step(code,ref predictor,ref index)/32768f;
        }
        return true;
    }
}
