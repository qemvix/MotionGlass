cbuffer Params : register(b0) {
    float2 Size; float2 InvSize;
    float Amount; float Delta; float Scaling; float Mode;
    float2 CursorPosition; float2 CursorSize;
    float Rotation; float CursorVisible; float FlowStep; float HasCoarse;
};
Texture2D<float4> Current : register(t0);
Texture2D<float4> Previous : register(t1);
Texture2D<float4> Older : register(t2);
Texture2D<float4> Oldest : register(t3);
Texture2D<float4> Pointer : register(t4);
Texture2D<float4> Flow : register(t5);
Texture2D<float4> Accumulation : register(t6);
SamplerState LinearClamp : register(s0);
struct Vertex { float4 position : SV_Position; float2 uv : TEXCOORD0; };
Vertex VS(uint id : SV_VertexID) {
    Vertex o;
    o.uv = float2((id << 1) & 2, id & 2);
    o.position = float4(o.uv * float2(2,-2) + float2(-1,1), 0, 1);
    return o;
}
float4 NormalizePS(Vertex i) : SV_Target {
    float2 uv = i.uv;
    if (Rotation == 2) uv = float2(i.uv.y, 1-i.uv.x);
    if (Rotation == 3) uv = 1-i.uv;
    if (Rotation == 4) uv = float2(1-i.uv.y, i.uv.x);
    float4 color = Current.SampleLevel(LinearClamp, uv, 0);
    float2 p = (i.uv * Size - CursorPosition) / max(CursorSize, 1);
    if (CursorVisible > 0 && all(p >= 0) && all(p < 1)) {
        float4 c = Pointer.SampleLevel(LinearClamp, p, 0);
        color.rgb = c.rgb * c.a + color.rgb * (1-c.a);
    }
    return float4(color.rgb, 1);
}
float ErrorAt(float2 uv, float2 displacement) {
    float error = 0;
    // A cross-shaped patch rejects isolated color matches while keeping the flow pass light.
    [unroll] for(int j=0; j<5; ++j) {
        float2 offset = j==0 ? float2(0,0) : j==1 ? float2(FlowStep,0) : j==2 ? float2(-FlowStep,0) : j==3 ? float2(0,FlowStep) : float2(0,-FlowStep);
        float3 a = Current.SampleLevel(LinearClamp, uv + offset*InvSize, 0).rgb;
        float3 b = Previous.SampleLevel(LinearClamp, uv + (offset+displacement)*InvSize, 0).rgb;
        error += dot(abs(a-b), float3(.3,.5,.2));
    }
    return error / 5;
}
float4 FlowPS(Vertex i) : SV_Target {
    float2 base = HasCoarse > .5 ? Flow.SampleLevel(LinearClamp,i.uv,0).xy : float2(0,0);
    float best = ErrorAt(i.uv, 0);
    float baseline = best;
    float2 velocity = 0;
    [unroll] for(int y=-2; y<=2; ++y) [unroll] for(int x=-2; x<=2; ++x) {
        float2 v = base + float2(x,y)*FlowStep;
        float error = ErrorAt(i.uv,v) + dot(v,v)*.0000003;
        if(error < best) { best=error; velocity=v; }
    }
    float confidence = saturate((baseline-best)*12) * (1-saturate(best*3));
    if (baseline < .006) { velocity = 0; confidence=0; }
    return float4(velocity, confidence, 1);
}
float3 VelocityBlur(float2 uv, float exposure) {
    float4 motion = Flow.SampleLevel(LinearClamp,uv,0);
    // Dilate motion into nearby uncovered pixels so the trail extends behind moving edges.
    [unroll] for(int j=0; j<8; ++j) {
        float angle = j * .78539816;
        float2 offset = float2(cos(angle),sin(angle))*16*InvSize;
        float4 candidate = Flow.SampleLevel(LinearClamp,uv+offset,0);
        if (dot(candidate.xy,candidate.xy)*candidate.z > dot(motion.xy,motion.xy)*motion.z) motion=candidate;
    }
    float2 trail = motion.xy * InvSize * exposure * saturate(motion.z*3);
    float3 color = 0;
    [unroll] for(int k=0;k<12;++k) color += Current.SampleLevel(LinearClamp,uv-trail*(k/11.0),0).rgb;
    return color/12;
}
float4 BlurPS(Vertex i) : SV_Target {
    float3 current = Current.SampleLevel(LinearClamp,i.uv,0).rgb;
    if ((Mode == 0 || Mode >= 3) && Amount <= 0) return float4(current,1);
    float3 a = Previous.SampleLevel(LinearClamp,i.uv,0).rgb;
    float3 b = Older.SampleLevel(LinearClamp,i.uv,0).rgb;
    float3 c = Oldest.SampleLevel(LinearClamp,i.uv,0).rgb;
    float3 blended = current*.4 + a*.3 + b*.2 + c*.1;
    float3 result;
    if (Mode == 0) {
        float exposure = Amount*2;
        if (Scaling > .5) exposure *= clamp((1.0/60.0)/max(Delta,.001),.25,8);
        result = VelocityBlur(i.uv,exposure);
    } else if (Mode == 1) result = blended;
    else if (Mode == 2) result = lerp(blended,VelocityBlur(i.uv,1),.65);
    else {
        float retention = exp(-clamp(Delta,.001,.1) / (.012 + Amount*Amount*.48));
        float3 history = Accumulation.SampleLevel(LinearClamp,i.uv,0).rgb;
        result = Mode == 3 ? max(current,history*retention) : lerp(current,history,retention);
    }
    return float4(saturate(result),1);
}
float4 CopyPS(Vertex i) : SV_Target { return float4(Current.SampleLevel(LinearClamp,i.uv,0).rgb,1); }
