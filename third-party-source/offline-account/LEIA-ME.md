# Auxiliar separado de conta local offline

- Origem: NTPWEdit 0.7, Vadim Druzhin, e bibliotecas chntpw/ntreg de Petter Nordahl-Hagen.
- Página do autor: http://cdslow.org.ru/en/ntpwedit/
- Arquivo fonte: http://cdslow.org.ru/files/ntpwedit/ntpwedit-0.7-src.zip
- SHA-256 do arquivo fonte recebido: 26994F38618B3A44ABF5C48D678FD67E415A04373FB59D063C14A73D38B723A3
- O site do autor foi acessado por HTTP. Não se atribui assinatura digital do fornecedor aos binários compilados localmente.
- As fontes completas recebidas estão em ntpwedit-0.7, com seus avisos originais. GPL v2, LGPL e licença do LibTomCrypt acompanham as fontes.

O wrapper OuOOfflineAccount.c é GPL-2.0-only e implementa um processo de console separado. Recebe apenas list/clear/unlock e RID. Lê uma cópia chamada SAM no diretório de trabalho; exige marcador do OuO. Lista nome/RID/estado, sem mostrar hashes. A remoção de senha é explícita; a senha nova deve ser definida no Windows após entrar.

Compilar.cmd x64 e Compilar.cmd x86 usam Visual Studio 2022 com ferramentas C/C++ e Windows SDK. Ajuste somente o caminho do vcvarsall se sua instalação for outra. A compilação estática não exige instalar runtime VC++ no usuário final. A recompilação muda o hash: atualize os hashes fixados no cliente somente após revisar a própria compilação.

O cliente guarda as cópias em pasta acessível somente a administradores/SYSTEM e bloqueia a instalação do Windows em uso. O auxiliar não toma decisões nem encontra automaticamente o Windows de trabalho. Não altere o wrapper para editar o SAM ativo. Os testes de versão/marcador não são prova de recuperação de conta em todas as builds.
