﻿using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Access.Systems;
using Content.Shared.Backmen.Reinforcement;
using Content.Shared.Backmen.Reinforcement.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Reinforcement.UI;

[GenerateTypedNameReferences]
public sealed partial class ReinforcementConsoleWindow : FancyWindow
{
    private readonly EntityUid _owner;
    private readonly ReinforcementConsoleComponent _comp;
    private readonly string _stationName;
    private readonly IPrototypeManager _proto;

    public ReinforcementConsoleWindow(EntityUid owner, ReinforcementConsoleComponent comp, IPrototypeManager proto, string stationName)
    {
        _owner = owner;
        _comp = comp;
        _proto = proto;
        _stationName = stationName;

        RobustXamlLoader.Load(this);

        Brief.OnTextChanged += BriefOnOnTextChanged;
        StartMission.OnPressed += StartMissionOnOnPressed;

        var msg = new FormattedMessage();
        msg.AddText(stationName);
        StationName.SetMessage(msg);

        TeamMin.Text = Loc.GetString("reinforcement-team-size-min", ("num",comp.MinMembers));
        TeamMax.Text = Loc.GetString("reinforcement-team-size-max", ("num",comp.MaxMembers));

        OpenCentered();
    }

    private void StartMissionOnOnPressed(BaseButton.ButtonEventArgs obj)
    {
        OnBriefChange.Invoke(Brief.Text);
        OnStartCall.Invoke();
    }

    public event Action<uint,uint> OnKeySelected = (u, c) => { };
    public event Action<string> OnBriefChange = (b) => { };
    public event Action OnStartCall = () => { };

    public void Update(UpdateReinforcementUi updateReinforcementUi)
    {
        var isActive = updateReinforcementUi.IsActive;

        // update brief
        Brief.Text = updateReinforcementUi.Brief;

        // reset grid rows
        SourcesList.RemoveAllChildren();
        if (isActive)
        {
            StartMission.Visible = false;
            Brief.Editable = false;
            CallByBox.Visible = true;
            CallBy.Text = updateReinforcementUi.CalledBy;

            foreach (var row in updateReinforcementUi.Members)
            {
                var r = new ActiveReinforcementRow()
                {
                    Id = row.id,
                    Owner = row.owner,
                    OwnerName = row.name,
                    Model = _comp.GetById(row.id,_proto),
                };

                var jobTitle = "";
                if (r.Model != null && _proto.TryIndex(r.Model.Job, out var job))
                {
                    jobTitle = Loc.GetString(job.Name);
                }

                var msg = FormattedMessage.FromMarkup(Loc.GetString("reinforcement-active-row",("name", row.name), ("state", (int)row.status), ("job",jobTitle)));
                r.TextInfo.SetMessage(msg);

                SourcesList.AddChild(r);
            }
            // disable button send
        }
        else
        {
            StartMission.Visible = true;
            Brief.Editable = true;
            CallByBox.Visible = false;
            CallBy.Text = "";
            var counts = new Dictionary<uint, int>();
            foreach (var (member, _, _, _) in updateReinforcementUi.Members)
            {
                counts.TryAdd(member, 0);
                counts[member]++;
            }

            foreach (var (member,proto) in _comp.Available.Select((x,i)=>(i,x)))
            {
                var cur = counts.GetValueOrDefault((uint) member, 0);
                var r = new ReinforcementRow()
                {
                    Id = (uint)member,
                    Model = _proto.Index(proto),
                    ModelCount = cur
                };
                var jobTitle = "";
                if (r.Model != null && _proto.TryIndex(r.Model.Job, out var job))
                {
                    jobTitle = Loc.GetString(job.Name);
                }

                var max = r.Model?.MaxCount ?? 1;
                var min = r.Model?.MinCount ?? 0;

                if (cur < min)
                {
                    OnKeySelected.Invoke((uint)member, (uint)min);
                    cur = min;
                }

                r.JobName.Text = Loc.GetString("reinforcement-row", ("name", Loc.GetString(r.Model!.Name)), ("job", jobTitle));
                r.Count.Text = r.ModelCount+"x";
                r.MinusBtn.OnPressed += args =>
                {
                    cur = Math.Max(min, cur-1);
                    OnKeySelected.Invoke((uint)member, (uint)cur);
                    r.Count.Text = cur+"x";
                    r.ModelCount = cur;
                    UpdateRows();
                };
                r.PlusBtn.OnPressed += args =>
                {
                    cur = Math.Min(max, cur+1);
                    OnKeySelected.Invoke((uint)member, (uint)cur);
                    r.Count.Text = cur+"x";
                    r.ModelCount = cur;
                    UpdateRows();
                };
                SourcesList.AddChild(r);
            }
            // render grid row with plus minus id
            // hide callBy
            // enable button send
        }
        UpdateRows();
    }

    private void UpdateRows()
    {
        var min = _comp.MinMembers;
        var max = _comp.MaxMembers;
        var cur = 0;
        foreach (var row in SourcesList.Children)
        {
            if (row is not ReinforcementRow r)
            {
                continue;
            }

            cur += r.ModelCount;
        }

        foreach (var row in SourcesList.Children)
        {
            if (row is not ReinforcementRow r)
            {
                continue;
            }

            r.PlusBtn.Disabled = max <= cur || r.Model?.MaxCount <= r.ModelCount;
        }
    }

    private void BriefOnOnTextChanged(LineEdit.LineEditEventArgs args)
    {
        OnBriefChange.Invoke(args.Text);
    }
}
