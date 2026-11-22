# Auxiliares de verificação

Estes arquivos são auxiliares de teste e não entram no executável principal. O script Compilar.ps1 compila apenas os arquivos .cs diretamente em source.

**StartupExitProbe.cs** reproduz a criação da janela principal e a recusa no aviso inicial. Argumentos: caminho do OuO.exe, caminho do relatório, modo `button` ou `close`, e `--preview`. O parâmetro `--preview` mantém o histórico do ensaio na pasta de diagnósticos do auxiliar; o cenário exercita a abertura real com recusa. Acrescente `--tray` para ensaiar esse modo. O resultado exige janela encerrada, ausência de exceção e monitoramento não iniciado.

**CloudAccountProbe.cs** consulta a conexão OneDrive já autorizada no perfil do Windows usando o motor da compilação informada. Argumentos: caminho do OuO.exe e caminho do relatório JSON. O relatório contém apenas totais e contagens, com conferência da ordem por tamanho. A consulta é de leitura e pode levar vários minutos em contas grandes. Não baixe nem altere conteúdo para preparar esse ensaio.

Compile os auxiliares com o compilador .NET Framework do Windows. Use `/win32manifest:source/app.manifest` no CloudAccountProbe para que o processo de teste declare a compatibilidade do Windows usada pelo OuO. O StartupExitProbe requer referências WPF; o CloudAccountProbe usa System.Web.Extensions. Verifique o executável compilado com a proteção instalada antes de executá-lo.

Não distribua arquivos de autorização, relatórios privados de conta ou inventários pessoais com o pacote público.
