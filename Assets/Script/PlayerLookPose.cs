using System.Collections.Generic;
using UnityEngine;

// Aim the animated face along the view direction, using the avatar's neutral
// head axes rather than adding camera pitch to an already tilted idle pose.
public sealed class PlayerLookPose
{
    private sealed class Bone
    {
        public Transform transform;
        public float weight;
        public Quaternion animation;
        public bool applied;
    }
    private readonly List<Bone> bones = new List<Bone>();
    private readonly Transform head;
    private readonly Vector3 faceForward;
    private float totalWeight, pitch, yaw;

    public PlayerLookPose(Animator animator)
    {
        if(animator==null || animator.avatar==null || !animator.isHuman)return;
        var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest)??
            animator.GetBoneTransform(HumanBodyBones.Chest)??animator.GetBoneTransform(HumanBodyBones.Spine);
        Add(chest,.2f);
        Add(animator.GetBoneTransform(HumanBodyBones.Neck),.2f);
        head=animator.GetBoneTransform(HumanBodyBones.Head);
        Add(head,.6f);
        if(head!=null)
        {
            // HumanDescription stores the imported bind pose even after Rebind
            // or animation evaluation changes the live transforms.
            var bind=new Dictionary<string,Quaternion>();
            foreach(var bone in animator.avatar.humanDescription.skeleton)
                bind[bone.name]=bone.rotation;
            var neutral=Quaternion.identity;
            for(var node=head;node!=null && node!=animator.transform;node=node.parent)
                neutral=(bind.TryGetValue(node.name,out var rotation)?rotation:node.localRotation)*neutral;
            faceForward=Quaternion.Inverse(neutral)*Vector3.forward;
        }
    }
    private void Add(Transform bone,float weight)
    {
        if(bone==null)return;
        bones.Add(new Bone{transform=bone,weight=weight});totalWeight+=weight;
    }
    public void RestoreAnimation()
    {
        foreach(var bone in bones)
        {
            if(bone.applied && bone.transform!=null)bone.transform.localRotation=bone.animation;
            bone.applied=false;
        }
    }
    public void Apply(Transform body,Vector3 worldDirection)
    {
        if(body==null || head==null || totalWeight<=0 || worldDirection.sqrMagnitude<.001f)return;
        var direction=body.InverseTransformDirection(worldDirection.normalized);
        float targetYaw=Mathf.Clamp(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-70,70);
        float targetPitch=Mathf.Clamp(-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg,-80,80);
        float blend=1-Mathf.Exp(-18*Mathf.Max(0,Time.deltaTime));
        pitch=Mathf.LerpAngle(pitch,targetPitch,blend);yaw=Mathf.LerpAngle(yaw,targetYaw,blend);
        var target=body.TransformDirection(Quaternion.Euler(pitch,yaw,0)*Vector3.forward);
        var correction=Quaternion.FromToRotation(head.TransformDirection(faceForward),target);
        foreach(var bone in bones)
        {
            if(bone.transform==null)continue;
            bone.animation=bone.transform.localRotation;bone.applied=true;
            float weight=bone.weight/totalWeight;
            // Parent turns also move the head, so these fractions sum to one
            // correction. Neutral gaze stays level despite the current clip.
            var turn=Quaternion.SlerpUnclamped(Quaternion.identity,correction,weight);
            bone.transform.rotation=turn*bone.transform.rotation;
        }
    }

    public static bool TryGetEyePosition(Animator animator,out Vector3 position)
    {
        position=Vector3.zero;
        if(animator==null || !animator.isHuman)return false;
        var left=animator.GetBoneTransform(HumanBodyBones.LeftEye);
        var right=animator.GetBoneTransform(HumanBodyBones.RightEye);
        if(left!=null || right!=null)
        {
            position=left!=null && right!=null?(left.position+right.position)*.5f:(left??right).position;
            return true;
        }
        // The customized character shares a rig with the original eye mesh;
        // it has no humanoid eye bones. Sample that skinned mesh once, even
        // when cosmetics hide it. Renderer bounds alone aren't pose accurate.
        int count=0;
        var mesh=new Mesh();
        try
        {
            foreach(var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string name=skin.name.ToLowerInvariant();
                if(skin.sharedMesh==null || !name.Contains("eye") || name.Contains("brow"))continue;
                mesh.Clear();skin.BakeMesh(mesh);
                position+=skin.transform.TransformPoint(mesh.bounds.center);count++;
            }
        }
        finally { Object.Destroy(mesh); }
        if(count>0){position/=count;return true;}
        var head=animator.GetBoneTransform(HumanBodyBones.Head);
        if(head==null)return false;
        foreach(var child in head.GetComponentsInChildren<Transform>(true))
            if(child.name.IndexOf("HeadTop",System.StringComparison.OrdinalIgnoreCase)>=0)
            {position=Vector3.Lerp(head.position,child.position,.5f);return true;}
        return false;
    }
    public void Reset() { RestoreAnimation();pitch=yaw=0; }
}
