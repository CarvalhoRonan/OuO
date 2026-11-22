import json,pathlib,re,hashlib,shutil,xml.etree.ElementTree as E
root=pathlib.Path(__file__).resolve().parents[1]; r=root/'work/tweak-research'; app=root/'outputs/Aurora'; rows=[]; report=[]
cat={'Privacy & Suggested Content':'privacy','AI':'privacy','System':'system','Start Menu & Search':'interface','Taskbar':'interface','Appearance':'visual','File Explorer':'explorer','Gaming':'gaming','Multi-tasking':'interface','Windows Update':'network','Other':'system'}
aliases={'DisableTelemetry':'telemetry','DisableSuggestions':'app-suggestions','DisableNotifications':'notification-center','DisableLocationServices':'location','DisableEdgeAds':'edge-promotions','DisableCopilot':'copilot-policy','DisableDVR':'game-recording','DisableStartRecommended':'start-recommendations','DisableBing':'start-bing','DisableMouseAcceleration':'mouse-acceleration','DisableStickyKeys':'sticky-shortcut','HideSearchTb':'task-search','HideTaskview':'task-view','DisableStorageSense':'storage-sense','EnableEndTask':'task-end','ShowKnownFileExt':'file-extensions','ShowHiddenFolders':'hidden-files','DisableTransparency':'transparency','DisableAnimations':'minimize-animation','DisableDeliveryOptimization':'delivery-peers','DisableDeviceAutoAppDownload':'device-companion','DisableBraveBloat':'brave-extras'}
def cs(v):return json.dumps(str(v),ensure_ascii=False)
def hivepath(v):
 h,p=v.split('\\',1);h={'HKEY_LOCAL_MACHINE':'HKLM','HKEY_CURRENT_USER':'HKCU','HKEY_CLASSES_ROOT':'HKCR','HKEY_USERS':'HKU'}.get(h,h.replace(':',''));return h,p
def setting(h,p,n='',v=0,k='DWord',delete=False,tree=False):return dict(Hive=h,Path=p,Name=n,Value=v,Kind=k,Delete=delete,DeleteTree=tree)
def regfile(file):
 b=file.read_bytes();s=b.decode('utf-16' if b[:2].hex() in ['fffe','feff'] else 'utf-8-sig');s=re.sub(r'\\\s*\r?\n\s*','',s); out=[];h=p=None; empty=False
 def flush():
  if empty:out.append(setting(h,p,'','','String'))
 for line in s.splitlines():
  line=line.strip()
  if not line or line.startswith(';') or line.startswith('Windows Registry'):continue
  if line.startswith('['):
   flush();empty=False;key=line[1:-1];d=key.startswith('-');h,p=hivepath(key.lstrip('-'))
   if d:out.append(setting(h,p,tree=True))
   else:empty=True
   continue
  m=re.fullmatch(r'(@|"(?:\\.|[^"\\])*")\s*=\s*(.*)',line)
  if not m:raise ValueError((file,line))
  n='' if m[1]=='@' else json.loads(m[1]);v=m[2];empty=False
  if v=='-':out.append(setting(h,p,n,delete=True));continue
  if v.lower().startswith('dword:'):
   x=int(v[6:],16);x=x-2**32 if x>=2**31 else x;out.append(setting(h,p,n,x));continue
  if v.startswith('"'):out.append(setting(h,p,n,json.loads(v),'String'));continue
  if v.lower().startswith('hex:'):out.append(setting(h,p,n,list(bytes.fromhex(v[4:].replace(',',''))),'Binary'));continue
  raise ValueError((file,v))
 flush();return out
f=json.loads((r/'raphire-src/Win11Debloat-master/Config/Features.json').read_text(encoding='utf-8-sig')); pt=json.loads((r/'raphire-src/Win11Debloat-master/Config/Languages/pt-BR/Features.json').read_text(encoding='utf-8-sig'))['Features']
for x in f['Features']:
 if not x.get('RegistryKey'):continue
 id=x['FeatureId'];lang=pt.get(id,{});settings=regfile(r/'raphire-src/Win11Debloat-master/Regfiles'/x['RegistryKey'])
 # Keep protected OS files hidden: the feature asks for hidden user folders, not protected files.
 if id=='ShowHiddenFolders':settings=[s for s in settings if s['Name']!='ShowSuperHidden']
 rows.append(dict(Id='raphire-'+id,Merge=aliases.get(id),Category=cat.get(x['Category'],'system'),Pt=lang.get('Label',x['Label']),En=x['Label'],DescriptionPt=lang.get('ToolTip','Preferência do Windows. Pode exigir encerrar a sessão ou reiniciar. Políticas da organização e a edição podem prevalecer.'),DescriptionEn=x.get('ToolTip','Windows preference; sign-out or restart may be needed. Edition and organizational policies can override it.'),MinBuild=x.get('MinVersion') or 10240,MaxBuild=x.get('MaxVersion') or 2147483647,Source='Raphire / Win11Debloat',Settings=settings))
ct=json.loads((r/'winutil-src/winutil-main/config/tweaks.json').read_text(encoding='utf-8-sig'),strict=False)
ctmeta={
'WPFTweaksActivity':('activity-history','privacy','Desativar publicação do histórico de atividades'),
'WPFTweaksRevertStartMenu':(None,'interface','Solicitar layout anterior do Iniciar (25H2)'),
'WPFTweaksDisableWarningForUnsignedRdp':('rdp-unsigned-warning','system','Reduzir avisos de arquivos RDP não assinados'),
'WPFTweaksEdgeDebloat':('edge-promotions','privacy','Reduzir publicidade e recursos opcionais do Edge'),
'WPFTweaksBraveDebloat':('brave-extras','privacy','Reduzir recursos opcionais do Brave'),
'WPFTweaksConsumerFeatures':('consumer-content','privacy','Reduzir conteúdo promocional'),
'WPFTweaksTelemetry':('telemetry','privacy','Reduzir diagnósticos e telemetria'),
'WPFTweaksLocation':('location','privacy','Restringir localização'),
'WPFTweaksDeliveryOptimization':('delivery-peers','network','Desativar compartilhamento de atualizações'),
'WPFTweaksUTC':('rtc-utc','system','Interpretar relógio de hardware como UTC'),
'WPFTweaksRemoveHomeAndGallery':(None,'explorer','Ocultar Início e Galeria no Explorador'),
'WPFTweaksDisplayBestPerformance':('visual-best','visual','Priorizar desempenho dos efeitos visuais'),
'WPFTweaksWindowsAI':(None,'privacy','Desativar recursos de IA do Windows por políticas'),
'WPFTweaksWPBT':('wpbt','system','Desativar execução via WPBT'),
'WPFTweaksPreventDeviceMetadata':('device-companion','system','Impedir aplicativos acompanhantes de dispositivos'),
'WPFTweaksRazerBlock':(None,'system','Bloquear instalação automática do Razer'),
'WPFTweaksDisableNotifications':('notification-center','interface','Desativar central de notificações'),
'WPFTweaksRightClickMenu':(None,'explorer','Usar menu de contexto clássico'),
'WPFTweaksDisableBGapps':('background-apps','performance','Restringir aplicativos em segundo plano'),
'WPFTweaksDetailedBSoD':('bsod-parameters','system','Mostrar detalhes técnicos na tela azul'),
'WPFTweaksBatteryPercentage':(None,'interface','Mostrar porcentagem de bateria na bandeja'),
'WPFTweaksDarkMode':(None,'visual','Ativar tema escuro do Windows'),
'WPFTweaksVerboseLogon':('login-verbose','interface','Mostrar mensagens detalhadas na entrada'),
'WPFTweaksNewOutlook':('new-outlook','interface','Solicitar o novo Outlook'),
'WPFTweaksStandbyFix':(None,'system','Desativar rede em espera conectada'),
'WPFTweaksS3Sleep':(None,'system','Solicitar suspensão S3 (requer firmware compatível)'),
'WPFTweaksHideSettingsHome':(None,'interface','Ocultar página inicial de Configurações'),
'WPFTweaksBing':('start-bing','interface','Ocultar sugestões da web no Iniciar'),
'WPFTweaksLoginBlur':('login-blur','interface','Remover desfoque da tela de entrada'),
'WPFTweaksLockscreen':(None,'interface','Desativar tela de bloqueio'),
'WPFTweaksStartRecommendations':('start-recommendations','interface','Ocultar recomendações do Iniciar'),
'WPFTweaksGameMode':('game-mode','gaming','Ativar Modo de Jogo'),
'WPFTweaksLongPaths':('long-paths','explorer','Permitir caminhos longos'),
}
actual={'WPFTweaksDisplayBestPerformance':'WPFTweaksDisplay','WPFTweaksPreventDeviceMetadata':'WPFTweaksPreventDeviceMetadataFromNetwork','WPFTweaksDetailedBSoD':'WPFToggleDetailedBSoD','WPFTweaksBatteryPercentage':'WPFToggleBatteryPercentage','WPFTweaksDarkMode':'WPFToggleDarkMode','WPFTweaksVerboseLogon':'WPFToggleVerboseLogon','WPFTweaksNewOutlook':'WPFToggleNewOutlook','WPFTweaksStandbyFix':'WPFToggleStandbyFix','WPFTweaksS3Sleep':'WPFToggleS3Sleep','WPFTweaksHideSettingsHome':'WPFToggleHideSettingsHome','WPFTweaksBing':'WPFToggleBingSearch','WPFTweaksLoginBlur':'WPFToggleLoginBlur','WPFTweaksLockscreen':'WPFToggleDisableLockscreen','WPFTweaksStartRecommendations':'WPFToggleStartMenuRecommendations','WPFTweaksGameMode':'WPFToggleGameMode','WPFTweaksLongPaths':'WPFToggleLongPaths'}
for id,(merge,category,label) in ctmeta.items():
 id=actual.get(id,id)
 if id not in ct:continue
 x=ct[id]; settings=[]
 for v in x.get('registry',[]):
  if 'Value' not in v:continue
  h,p=hivepath(v['Path'].replace(':\\','\\')); k=v.get('Type','DWord');value=v['Value'];delete=value=='<RemoveEntry>'
  if k.lower() in ['dword','qword'] and not delete:value=int(value,0) if isinstance(value,str) and value.lower().startswith('0x') else int(value)
  settings.append(setting(h,p,v['Name'],value,k,delete))
 if settings:
  minimum=22000 if id in ['WPFToggleBatteryPercentage','WPFTweaksRemoveHomeAndGallery','WPFTweaksRevertStartMenu','WPFToggleStartMenuRecommendations','WPFTweaksWindowsAI'] else 10240
  maximum=26200 if id=='WPFTweaksRevertStartMenu' else 2147483647
  rows.append(dict(Id='ctt-'+id,Merge=merge,Category=category,Pt=label,En=x['Content'],DescriptionPt='Ajuste avançado baseado no WinUtil. O valor anterior será salvo. Pode exigir reiniciar; a edição e a versão do Windows podem limitar o efeito.'+(' S3 só funciona quando o firmware oferece esse modo; o ajuste pode remover a suspensão quando S3 não está disponível.' if id=='WPFToggleS3Sleep' else ''),DescriptionEn=x.get('Description',''),MinBuild=minimum,MaxBuild=maximum,Source='Chris Titus / WinUtil',Settings=settings))
# Registry-backed DISM++ equivalents. Current-user choices only; avoid duplicate default-profile controls.
data=E.parse(r/'Dism-Data.xml').getroot();maps={x.get('Target'):x.get('Link') for x in E.parse(r/'Dism-pt.xml').iter('String')};eng={x.get('Target'):x.get('Link') for x in E.parse(r/'Dism-en.xml').iter('String')}
def trans(v):
 v=maps.get((v or '').lstrip('#'),v or '');v=re.sub(r'\s*\(by[^)]*\)','',v.replace('（','(').replace('）',')'),flags=re.I).strip()
 return {'#禁用Win11加入的新右键菜单，默认显示更多选项':'Usar menu de contexto clássico no Windows 11','#默认输入模式：':'Modo padrão do teclado IME','#中文模式':'Chinês','#英数模式':'Alfanumérico'}.get(v,v)
for gi,g in enumerate(data.find('SystemOptimization')):
 for ii,item in enumerate(g.findall('Item')):
  scope=item.find('Current')
  if scope is None:scope=item
  acts=scope.findall('Dropdown') if item.get('Type')=='Combo' else [scope.find('True')]
  for ai,a in enumerate(acts):
   if a is None or a.find('Activate') is None:continue
   settings=[];name=trans(item.get('Name'));option=trans(a.get('Name'));custom=False
   for op in a.find('Activate'):
    if op.tag not in ['RegWrite','RegDelete']:continue
    h,p=hivepath(op.get('Key'));k={'REG_DWORD':'DWord','REG_QWORD':'QWord','REG_SZ':'String','REG_EXPAND_SZ':'ExpandString','REG_BINARY':'Binary'}.get(op.get('Type'),'String');v=op.get('Data','');delete=op.tag=='RegDelete';tree=delete and op.get('Value') is None
    if v.startswith('?'):custom=True;v=0xff3d8c80;k='DWord'
    if k=='DWord':v=int(v,16) if isinstance(v,str) else v;v=v-2**32 if v>=2**31 else v
    elif k=='Binary':v=list(bytes.fromhex(v))
    if op.get('Value')=='JPEGImportQuality' and not delete:v=60 if ai==0 else 100
    settings.append(setting(h,p,op.get('Value',''),v,k,delete and not tree,tree))
   if not settings:continue
   label=name+(' • '+option if option else '')
   if any(s['Name']=='JPEGImportQuality' and not s['Delete'] for s in settings):label='Qualidade do papel de parede JPEG: '+str(settings[0]['Value'])+'%'
   if custom:label='Aplicar verde OuO à barra de título inativa'
   # Translate known Chinese fallback independently of outdated translation files.
   if '#' in label:raise ValueError(label)
   minbuild=22000 if 'Win11' in (item.get('Name') or '') else 7600
   maxbuild=19045 if any('Internet Explorer' in s['Path'] for s in settings) else 2147483647
   # Classic taskbar preferences do not control the rewritten Windows 11 taskbar.
   if any(s['Name'] in ['TaskbarSizeMove','TaskbarSmallIcons','EnableAutoTray'] for s in settings):maxbuild=19045
   desc='Preferência equivalente ao catálogo Dism++. O estado anterior será salvo. Pode requerer encerrar a sessão. Recursos antigos só têm efeito em aplicativos e versões compatíveis.'
   if any(s['Name'] in ['ModRiskFileTypes','SaveZoneInformation'] for s in settings):desc+=' Atenção: reduz avisos de segurança de arquivos obtidos da internet. Mantenha desmarcado se não tiver necessidade específica.'
   if any(s['Name']=='LoggingDisabled' for s in settings):desc+=' Reduz os registros de falhas e pode dificultar o diagnóstico de problemas.'
   en=eng.get(item.get('Name','').lstrip('#'),label);en=re.sub(r'\s*[（(]by[^)）]*[)）]','',en,flags=re.I).strip()
   if option:en+=' • '+eng.get((a.get('Name') or '').lstrip('#'),option)
   rows.append(dict(Id=f'dism-{gi}-{ii}-{ai}',Merge=None,Category='explorer' if any('Explorer' in s['Path'] for s in settings) else 'system',Pt=label,En=en,DescriptionPt=desc,DescriptionEn='DISM++ catalog equivalent. Previous state is backed up; sign-out may be required. Legacy settings only affect compatible applications and versions.',MinBuild=minbuild,MaxBuild=maxbuild,Source='Chuyu-Team / Dism++ Data',Settings=settings))
def value(v,k):
 if isinstance(v,list):return 'new byte[]{'+','.join(str(x) for x in v)+'}'
 if isinstance(v,int):return str(v)+('L' if k=='QWord' else '')
 return cs(v)
def emit(s):return 'new TweakSetting{'+','.join([f'Hive={cs(s["Hive"])}',f'Path={cs(s["Path"])}',f'Name={cs(s["Name"])}',f'Value={value(s["Value"],s["Kind"])}',f'Kind=RegistryValueKind.{s["Kind"]}',f'Delete={str(s["Delete"]).lower()}',f'DeleteTree={str(s["DeleteTree"]).lower()}'])+'}'
lines=['// Generated from licensed catalog snapshots; see third-party-source/tweaks and work/generate-tweaks.py.','using System; using System.Collections.Generic; using System.Linq; using Microsoft.Win32;','namespace Aurora { public static partial class TweakCatalog {','static void Supplement(List<Tweak> list){']
for row in rows:
 fields=[f'{k}={cs(row[k])}' for k in ['Id','Category','Pt','En','DescriptionPt','DescriptionEn']]+[f'MinBuild={row["MinBuild"]}',f'MaxBuild={row["MaxBuild"]}',f'Source={cs(row["Source"])}']
 lines.append('MergeCatalog(list,'+(cs(row['Merge']) if row['Merge'] else 'null')+',new Tweak{'+','.join(fields)+'},new TweakSetting[]{'+','.join(emit(s) for s in row['Settings'])+'});')
lines+=['}', '''static void MergeCatalog(List<Tweak> list,string merge,Tweak incoming,TweakSetting[] settings){
var existing=merge==null?null:list.FirstOrDefault(x=>x.Id==merge);
if(existing!=null){var combined=existing.Settings().ToList();foreach(var s in settings)if(!combined.Any(x=>TweakRegistryEngine.Identity(x)==TweakRegistryEngine.Identity(s)))combined.Add(s);existing.Extra=combined.Skip(1).ToArray();existing.Source=String.Join("; ",new[]{existing.Source,incoming.Source}.Where(x=>!String.IsNullOrEmpty(x)).Distinct());return;}
var normalized=settings.GroupBy(TweakRegistryEngine.Identity).Select(g=>g.Last()).ToArray();if(normalized.Length==0)return;
if(list.Any(x=>x.Settings().Count()==normalized.Length&&x.Settings().All(a=>normalized.Any(b=>TweakRegistryEngine.Identity(a)==TweakRegistryEngine.Identity(b)&&TweakRegistryEngine.Same(a,b)))))return;
var primary=normalized[0];incoming.Hive=primary.Hive;incoming.Path=primary.Path;incoming.Name=primary.Name;incoming.Value=primary.Value;incoming.Kind=primary.Kind;incoming.Delete=primary.Delete;incoming.DeleteTree=primary.DeleteTree;incoming.Extra=normalized.Skip(1).ToArray();list.Add(incoming);
}''','}}']
(app/'source/TweakCatalogSupplement.cs').write_text('\n'.join(lines),encoding='utf-8-sig')
(r/'catalog-normalized.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
dst=app/'third-party-source/tweaks';dst.mkdir(parents=True,exist_ok=True)
for sub in ['raphire','winutil','dism']: (dst/sub).mkdir(exist_ok=True)
for src,name in [('raphire-src/Win11Debloat-master/LICENSE','Raphire-Win11Debloat-MIT.txt'),('winutil-src/winutil-main/LICENSE','WinUtil-MIT.txt'),('Dism-LICENSE.txt','Dism-Data-MIT.txt')]:shutil.copyfile(r/src,app/'licenses'/name)
shutil.copyfile(r/'raphire-src/Win11Debloat-master/Config/Features.json',dst/'raphire/Features.json');shutil.copyfile(r/'raphire-src/Win11Debloat-master/Config/Languages/pt-BR/Features.json',dst/'raphire/pt-BR.json')
shutil.copytree(r/'raphire-src/Win11Debloat-master/Regfiles',dst/'raphire/Regfiles',dirs_exist_ok=True)
for src,target in [('winutil-src/winutil-main/config/tweaks.json','winutil/tweaks.json'),('Dism-Data.xml','dism/Data.xml'),('Dism-pt.xml','dism/pt.xml'),('Dism-en.xml','dism/en.xml'),('catalog-normalized.json','catalog-normalized.json')]:shutil.copyfile(r/src,dst/target)
shutil.copyfile(__file__,dst/'generate-tweaks.py')
print('Generated',len(rows),'source definitions,',sum(len(x['Settings']) for x in rows),'registry operations before merging/deduplication')
