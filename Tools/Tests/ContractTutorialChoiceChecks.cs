using System;

internal static class ContractTutorialChoiceChecks
{
    private static int checks;
    private static void Check(bool result,string description)
    {
        checks++;
        if(!result)throw new Exception(description);
    }
    public static int Main()
    {
        var choice=new ContractTutorialPolicy();
        for(int level=1;level<=5;level++)
        {
            choice.Reset();
            Check(!choice.HasChoice(level),"New attempt must ask: "+level);
            choice.Choose(level,10,false);
            Check(choice.Skip(level) && !choice.Restart(level,10),"Skip must bypass the current lesson: "+level);
            Check(choice.Skip(level),"Entering the editor must retain skip: "+level);
            Check(!choice.Skip(level==5?1:level+1),"Skip must not leak into another contract: "+level);
            choice.Forget(level);
            Check(!choice.HasChoice(level),"Retry must ask again: "+level);
            choice.Choose(level,10,true);
            Check(choice.Restart(level,10) && !choice.Skip(level),"Yes must replay studio guidance: "+level);
            Check(!choice.Restart(level,20) && choice.HasChoice(level),"Editor/return must retain the answer without restarting the studio lesson: "+level);
            choice.Forget(level==5?1:level+1);
            Check(choice.HasChoice(level),"Unrelated retry must not erase this attempt's choice: "+level);
        }
        choice.Choose(2,10,false);
        choice.Choose(3,10,true);
        Check(!choice.HasChoice(2) && !choice.Skip(2),"Next contract replaces the previous attempt's choice");
        choice.Reset();
        for(int level=1;level<=5;level++)Check(!choice.HasChoice(level),"New game clears all choices");
        foreach(int invalid in new[]{0,6})
        {
            bool rejected=false;
            try{choice.Choose(invalid,10,true);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected && !choice.HasChoice(invalid),"Invalid contract must not become a selected tutorial");
        }
        Console.WriteLine(checks+" tutorial choice checks passed.");
        return 0;
    }
}
