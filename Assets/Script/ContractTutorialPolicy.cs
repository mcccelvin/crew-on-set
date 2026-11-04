// Per-attempt choice only. No completed lessons, grades, purchases or saves live here.
public sealed class ContractTutorialPolicy
{
    private int level,scene;
    private bool selected,play;
    public bool HasChoice(int contract)=>selected && level==contract;
    public bool Skip(int contract)=>HasChoice(contract) && !play;
    public bool Restart(int contract,int studioScene)=>HasChoice(contract) && play && scene==studioScene;
    public void Choose(int contract,int studioScene,bool playTutorial)
    {
        if(contract<1 || contract>5)throw new System.ArgumentOutOfRangeException(nameof(contract));
        level=contract;scene=studioScene;play=playTutorial;selected=true;
    }
    public void Forget(int contract){if(HasChoice(contract))Reset();}
    public void Reset(){selected=false;level=scene=0;play=false;}
}
