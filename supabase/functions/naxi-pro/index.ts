////////////////////////////////////////////////////////////////////////////////
// sacrificial first line: the deploy pipeline trims a few leading bytes,
// so this comment absorbs it and the real code below stays intact.
// Naxi Pro / Naxi MAX — invite code service with configurable multi-use ("waves").
// Works in three modes:
//   1) Legacy (before 0001_pro_waves.sql): single-use rows on pro_invites (device_id / redeemed_at).
//   2) Waves (after migration): max_uses activations per code, bindings in pro_devices.
//   3) Naxi MAX (after 0003_max_invites.sql): codes in max_invites, bindings in max_devices.
// The tier is decided by which table the code lives in — never by the client.
// Deploy: Dashboard → Edge Functions → naxi-pro → replace code → Deploy
//     or: supabase functions deploy naxi-pro --project-ref kjvxulkfsyvmedanbsxg
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { ...cors, "Content-Type": "application/json" },
  });

const bad = (error: string) => json({ ok: false, error }, 400);

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  if (req.method !== "POST") return json({ ok: false, error: "method_not_allowed" }, 405);

  const admin = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
    { auth: { persistSession: false } },
  );

  let body: { action?: string; code?: string; device_id?: string };
  try {
    body = await req.json();
  } catch {
    return bad("invalid_body");
  }

  const device = (body.device_id ?? "").trim().toUpperCase();
  if (!/^[0-9A-F]{64}$/.test(device)) return bad("invalid_device");

  const action = (body.action ?? "redeem").toLowerCase();

  // Active bindings for this device across both plans; expired ones are lazily
  // removed (frees the code slot).
  const activeForDevice = async () => {
    const active: { code: string; expires_at: string | null; plan: "pro" | "max" }[] = [];
    for (const [plan, table] of [["pro", "pro_devices"], ["max", "max_devices"]] as const) {
      const a = await admin.from(table).select("code,expires_at").eq("device_id", device);
      if (a.error || !a.data) continue;
      for (const r of a.data) {
        if (r.expires_at && new Date(r.expires_at) <= new Date()) {
          await admin.from(table).delete().eq("code", r.code).eq("device_id", device);
        } else {
          active.push({ code: r.code, expires_at: r.expires_at, plan });
        }
      }
    }
    return active;
  };

  if (action === "status") {
    const mine = await activeForDevice();
    if (mine.length > 0) {
      let exp: string | null = null;
      let isPro = false;
      let isMax = false;
      for (const m of mine) {
        if (m.expires_at && (exp === null || new Date(m.expires_at) > new Date(exp))) exp = m.expires_at;
        if (m.plan === "max") isMax = true;
        else isPro = true;
      }
      // pro:true is also sent for MAX-only devices so older builds keep working.
      return json({ ok: true, pro: isPro || isMax, max: isMax, ...(exp ? { expires_at: exp } : {}) });
    }
    // Legacy single-use bindings (device_id right on the invite row).
    const pb = await admin.from("pro_invites").select("code").eq("device_id", device).limit(1);
    const mb = await admin.from("max_invites").select("code").eq("device_id", device).limit(1);
    const isPro = !!(pb.data && pb.data.length > 0);
    const isMax = !!(mb.data && mb.data.length > 0);
    return json({ ok: true, pro: isPro || isMax, max: isMax });
  }

  // Any other action is treated as redeem (kept for client compatibility).
  const code = (body.code ?? "").trim().toUpperCase();
  if (!/^[A-Z0-9]{4,16}$/.test(code)) return bad("invalid");

  // The code's tier is decided by which table it lives in — never by the client.
  let plan: "pro" | "max" = "pro";
  let lookup = await admin.from("pro_invites").select("*").eq("code", code).maybeSingle();
  if (lookup.error || !lookup.data) {
    const mx = await admin.from("max_invites").select("*").eq("code", code).maybeSingle();
    if (mx.error) return bad("invalid");
    if (mx.data) {
      plan = "max";
      lookup = mx;
    }
  }
  if (lookup.error || !lookup.data) return bad("invalid");
  const row = lookup.data as Record<string, unknown>;

  const invitesTable = plan === "max" ? "max_invites" : "pro_invites";
  const devicesTable = plan === "max" ? "max_devices" : "pro_devices";

  const maxUses = typeof row.max_uses === "number" && (row.max_uses as number) > 0 ? (row.max_uses as number) : 1;
  const dur = typeof row.duration_days === "number" && (row.duration_days as number) > 0 ? (row.duration_days as number) : 0;

  // Already bound to this device?
  const mine = await admin.from(devicesTable).select("expires_at").eq("code", code).eq("device_id", device).maybeSingle();
  if (!mine.error && mine.data) {
    if (mine.data.expires_at && new Date(mine.data.expires_at) <= new Date()) return bad("expired");
    return json({ ok: true, already: true, pro: true, max: plan === "max", ...(mine.data.expires_at ? { expires_at: mine.data.expires_at } : {}) });
  }
  if (typeof row.device_id === "string" && row.device_id.trim().toUpperCase() === device) {
    return json({ ok: true, already: true, pro: true, max: plan === "max" });
  }

  // Capacity check (waves): only ACTIVE bindings count — expired ones free the slot.
  const nowIso = new Date().toISOString();
  const cnt = await admin.from(devicesTable).select("*", { count: "exact", head: true }).eq("code", code).or(`expires_at.is.null,expires_at.gt.${nowIso}`);
  const used = !cnt.error && typeof cnt.count === "number"
    ? cnt.count
    : typeof row.uses_count === "number"
      ? (row.uses_count as number)
      : row.device_id
        ? 1
        : 0;
  if (used >= maxUses) return bad("used");

  const expiresAt = dur > 0 ? new Date(Date.now() + dur * 86400000).toISOString() : null;
  const ins = await admin.from(devicesTable).insert({ code, device_id: device, expires_at: expiresAt });
  if (ins.error) {
    const msg = (ins.error.message ?? "").toLowerCase();
    if (msg.includes("duplicate")) return json({ ok: true, already: true, pro: true, max: plan === "max" }); // race lost
    // No devices table (pre-migration) → legacy single-use binding.
    if (row.device_id) return bad("used");
    const up = await admin
      .from(invitesTable)
      .update({ device_id: device, redeemed_at: new Date().toISOString() })
      .eq("code", code);
    if (up.error) return json({ ok: false, error: "server" }, 500);
    return json({ ok: true, already: false, pro: true, max: plan === "max" });
  }

  await admin
    .from(invitesTable)
    .update({
      uses_count: used + 1,
      redeemed_at: row.redeemed_at ?? nowIso,
    })
    .eq("code", code);

  return json({ ok: true, already: false, pro: true, max: plan === "max", ...(expiresAt ? { expires_at: expiresAt } : {}) });
});
