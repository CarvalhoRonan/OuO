# Componentes de terceiros — OuO 1.1 • Overclock's Utility Optimizer

As bibliotecas e instaladores abaixo são distribuídos como arquivos separados. O auxiliar de recuperação offline é uma compilação local modificada, identificada em sua seção. O código do cliente Aurora e do auxiliar está em `source/` e permite substituir/recompilar os componentes de acordo com suas licenças.

## rclone 1.76.0

O analisador de armazenamento em nuvem baixa o executável portátil oficial na primeira conexão. O executável é distribuído pelo próprio projeto rclone. O OuO verifica o SHA-256 oficial do arquivo compactado e o SHA-256 do executável extraído. Os comandos de análise consultam cota, nomes e tamanhos em modo de leitura.

[Projeto e licença MIT](https://github.com/rclone/rclone/tree/v1.76.0) · [Downloads oficiais](https://rclone.org/downloads/) · [Hashes oficiais da versão](https://downloads.rclone.org/v1.76.0/SHA256SUMS)

## Libre Hardware Monitor 0.9.6

- Projeto: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- Versão: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6
- Pacote oficial de origem: `LibreHardwareMonitor.zip`.
- SHA-256 do pacote conferido contra o digest publicado pelo GitHub: `086d9f1b5a99e643edc2cfaaac16051685b551e4c5ac0b32a57c58c0e529c001`.
- Biblioteca e dependências do pacote em `Sensors/`; o aplicativo gráfico original não é iniciado nem necessário.
- Licença MPL-2.0: `licenses/LibreHardwareMonitor-MPL-2.0.txt`.
- Avisos de terceiros originais: `licenses/LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt`.
- Código da versão original incluído em `third-party-source/LibreHardwareMonitor-0.9.6-source.zip`.
- Arquivos auxiliares .NET e bibliotecas de hardware acompanham a distribuição oficial; seus nomes/hashes constam em `Sensors/files-sha256.csv`. Não são todos carregados em execução: apenas as dependências necessárias ao motor.

## PawnIO

- Site do autor: https://pawnio.eu/
- Driver: https://github.com/namazso/PawnIO/tree/2.2.0
- Instalador: https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0
- O instalador `Sensors/PawnIO_setup.exe` foi atualizado pela distribuição oficial do autor para 2.2.0; a biblioteca Libre Hardware Monitor permanece 0.9.6.
- Versão do instalador: 2.2.0.0. Assinatura Authenticode verificada como válida na preparação do pacote; editor `namazso`.
- SHA-256: `1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032`, correspondente ao digest oficial da release 2.2.0.
- Licença e exceção do projeto: `licenses/PawnIO-LICENSE.txt` e README contido no código original em `third-party-source/PawnIO-2.2.0-source.zip`.
- Repositório da distribuição do instalador incluído em `third-party-source/PawnIO.Setup-2.2.0-repository.zip`.
- Os módulos PawnIO embutidos na biblioteca Libre são componentes do projeto upstream, com avisos LGPL no documento original. As fontes de módulos ficam em https://github.com/namazso/PawnIO.Modules e os arquivos de referência estão em `third-party-source/`.

O OuO oferece a preparação do driver quando ausente e só abre o instalador após confirmação; não altera a integridade de memória do Windows nem configura exclusões de antivírus para liberar sensores.

## Ícones dos aplicativos

Os 13 ícones são utilizados exclusivamente para identificar os programas correspondentes. As marcas e os direitos sobre os desenhos permanecem com seus titulares; isso não representa endosso ao Aurora.

O arquivo `assets/icons/sources.json` registra o endereço oficial de cada arquivo e seu SHA-256, obtidos durante esta entrega. Origens: Mozilla/gecko-dev, Google Chrome, Brave/brave-core, The Document Foundation/LibreOffice, SumatraPDF, 7-Zip, Microsoft PowerToys, Notepad++, VideoLAN, Spotify, Discord, Microsoft VS Code e Git.

Os avisos de licença dos projetos que fornecem imagens acompanham esta pasta de licenças quando aplicáveis. O logotipo Git é atribuído a Jason Long, sob Creative Commons Attribution 3.0: https://git-scm.com/downloads/logos e https://creativecommons.org/licenses/by/3.0/ . Os pictogramas de navegação, sol/lua e ícone genérico são desenhos vetoriais implementados pelo próprio Aurora.

## Integrações opcionais externas

ClamAV: https://www.clamav.net/ e documentação em https://docs.clamav.net/. O OuO pode executar clamscan.exe/freshclam.exe selecionados pelo usuário e interpretar o resultado. Os binários, bibliotecas e bases de assinatura ClamAV não são distribuídos neste ZIP. A licença do motor e os termos da distribuição oficial continuam aplicáveis à instalação obtida pelo usuário.

Microsoft Defender e ferramentas nativas do Windows pertencem à instalação do sistema. AdwCleaner é obtido separadamente da Malwarebytes e tem sua assinatura verificada antes da execução. WinGet e Chocolatey mantêm seus próprios contratos, fontes e licenças dos pacotes.

## Referências de ajustes

A revisão 2 compila definições adaptadas dos seguintes catálogos, sob MIT:

- ChrisTitusTech/winutil: https://github.com/ChrisTitusTech/winutil — copyright CTTechGroupLLC. Licença: licenses/WinUtil-MIT.txt.
- Raphire/Win11Debloat: https://github.com/Raphire/Win11Debloat — copyright Raphire. Licença: licenses/Raphire-Win11Debloat-MIT.txt.
- Chuyu-Team/Dism-Multi-language: https://github.com/Chuyu-Team/Dism-Multi-language — copyright Chuyu-Team. Licença: licenses/Dism-Data-MIT.txt.

Os snapshots de configurações, traduções e arquivos .reg ficam em third-party-source/tweaks, com proveniência e SHA-256. As definições adaptadas estão em source/TweakCatalogSupplement.cs. O executor usa backup/restauração próprios, seleção por versão e validação de conflitos. As strings InvokeScript guardadas nos snapshots servem para consulta; as ações executáveis do OuO estão compiladas em seu código. O motor Dism++ não é distribuído.

## Catálogos de drivers

Microsoft Update Catalog, Dell CatalogPC e HP Client Catalog são consultados nos servidores oficiais. Os pacotes de drivers são baixados quando solicitados pelo usuário, com os termos dos respectivos fabricantes. O OuO verifica a assinatura do pacote OEM e do catálogo INF e usa o PnPUtil. Os catálogos completos e os executáveis de drivers não acompanham o ZIP. assets/test-fixtures contém pequenos excertos de metadados públicos para testes dos parsers. Origem e endereços estão em third-party-source/tweaks/PROVENIENCIA.json e REVISAO-1.0-RC4-r2.md.


RC5: a apresentação dos tweaks é consolidada por source/TweakPreferences.cs. Os snapshots e avisos MIT continuam incluídos. Os novos ícones de navegação são desenhos vetoriais do próprio OuO, definidos em source/NavigationIcons.cs.

## Auxiliar de recuperação offline — NTPWEdit/chntpw

- Executáveis separados compilados localmente: Tools/OfflineAccount/x64/OuO.OfflineAccount.exe e Tools/OfflineAccount/x86/OuO.OfflineAccount.exe.
- Base: NTPWEdit 0.7 (Vadim Druzhin), chntpw/ntreg (Petter Nordahl-Hagen); wrapper próprio GPL-2.0-only.
- Fonte completa, wrapper, instruções e licenças originais: third-party-source/offline-account/.
- GPL v2 e LGPL v2.1 também em licenses/NTPWEdit-GPL-2.0.txt e licenses/NTPWEdit-LGPL-2.1.txt; LibTomCrypt inclui sua licença nas fontes.
- Proveniência e hash do pacote fonte: third-party-source/offline-account/LEIA-ME.md. Os binários do fornecedor não são usados na distribuição; o auxiliar foi compilado das fontes e seu hash é fixado no cliente.
- Hashes de todos os arquivos distribuídos constam em ARQUIVOS-SHA256.csv. Nenhuma cópia de SAM/SYSTEM/SECURITY de teste ou chave privada de extensão é distribuída.
