using System;

// Main-thread producer, audio-thread consumer. Bounded latency, wrap-safe sequence
// checks and independent packet state prevent a lost frame from corrupting speech.
public sealed class CrewVoiceBuffer
{
    private readonly object sync=new object();
    private readonly float[] ring=new float[CrewVoiceCodec.SampleRate];
    private int read,write,count;
    private ushort last;
    private bool sequenced,primed;
    private float tail;
    public int Queued {get{lock(sync)return count;}}
    public void Clear()
    {lock(sync){read=write=count=0;primed=false;tail=0;}}
    public bool Push(float[] frame,ushort sequence)
    {
        if(frame==null || frame.Length!=CrewVoiceCodec.Samples)return false;
        lock(sync)
        {
            int distance=sequenced?(ushort)(sequence-last):1;
            if(distance==0 || distance>=32768)return false;
            if(distance>4){read=write=count=0;primed=false;}
            else for(int i=0;i<(distance-1)*CrewVoiceCodec.Samples;i++)Append(0);
            foreach(float value in frame)Append(value);
            if(count>CrewVoiceCodec.Samples*5)
            {int drop=count-CrewVoiceCodec.Samples*3;read=(read+drop)%ring.Length;count-=drop;}
            last=sequence;sequenced=true;return true;
        }
    }
    private void Append(float value)
    {
        if(count==ring.Length){read=(read+1)%ring.Length;count--;}
        ring[write]=value;write=(write+1)%ring.Length;count++;
    }
    public void Read(float[] output)
    {
        lock(sync)
        {
            Array.Clear(output,0,output.Length);
            if(!primed && count<CrewVoiceCodec.Samples*2)return;
            primed=true;int available=Math.Min(output.Length,count);
            for(int i=0;i<available;i++){output[i]=ring[read];read=(read+1)%ring.Length;}
            count-=available;
            if(available>0)tail=output[available-1];
            if(available<output.Length)
            {
                int fade=Math.Min(64,output.Length-available);
                for(int i=0;i<fade;i++)output[available+i]=tail*(1f-(i+1f)/fade);
                tail=0;primed=false;
            }
        }
    }
}
