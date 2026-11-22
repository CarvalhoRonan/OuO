# OuO — Overclock Utility Optimizer

Utilitário portátil para manutenção, diagnóstico e organização do Windows, desenvolvido pela **Overclock Informática**.

**Versão atual: 1.0.0.107 — prévia nuvem r5.** Esta entrega contém recursos em fase de testes, identificados dentro do aplicativo. A homologação completa ainda está em andamento.

## Download

[**Baixar OuO 1.0.0.107 — pacote completo**](https://github.com/CarvalhoRonan/OuO/releases/download/v1.0.0.107-preview/OuO-1.0.0-previa-nuvem-r5.zip)

1. Extraia o ZIP em uma pasta do computador.
2. Abra `OuO.exe` dentro da pasta extraída.
3. Leia o aviso inicial e as descrições dos recursos antes de aplicar alterações.

O pacote inclui o programa, suas dependências, o código-fonte e os avisos dos componentes de terceiros. Algumas operações de manutenção precisam de permissão de administrador. Os ensaios desta entrega foram realizados em Windows 11 x64; outros sistemas e conjuntos de hardware têm validação pendente.

## Recursos

- Visão do computador, armazenamento, memória e monitoramento de hardware.
- Processos, inicialização, estabilidade e eventos do Windows.
- Tweaks organizados por categoria, com checagem e restauração.
- Instalação e atualização de programas, Office e bibliotecas.
- Rede, DNS, bloqueios e navegação segura.
- Drivers, reparos, manutenção e recuperação do sistema.
- Análise de espaço no Google Drive e OneDrive.

Consulte [STATUS-DO-ESCOPO.md](STATUS-DO-ESCOPO.md) para conhecer as funcionalidades e as limitações de validação.

## Novidades desta prévia

- OneDrive: filtros de fotos/imagens e vídeos, maiores arquivos primeiro e navegação por páginas.
- Comparação dos tamanhos listados com a cota e a lixeira da conta.
- Consulta de metadados do histórico de versões do arquivo selecionado.
- Progresso da análise e filtros que aproveitam os resultados já carregados.
- Correção da classificação de arquivos TypeScript que poderiam aparecer como vídeos.
- Correção do erro de janela ao clicar em **Sair** no aviso inicial.

O analisador de nuvem consulta metadados em modo de leitura. O login acontece no navegador. O cadastro integrado do Google está em modo de testes, com acesso limitado às contas autorizadas nesse cadastro. A análise individual da biblioteca do Google Fotos ainda não foi implementada; veja [GOOGLE-FOTOS-OPCOES.md](GOOGLE-FOTOS-OPCOES.md).

## Validação e integridade

A compilação passou em **369 verificações automatizadas e 26 telas**, além de consultas reais de metadados do OneDrive e testes de encerramento no aviso inicial. Os cenários ainda sem homologação continuam identificados como **Recurso em fase de testes**.

A distribuição permanece **sem assinatura de código**. O executável e o ZIP desta compilação passaram por uma análise específica do Microsoft Defender na rodada de validação de 18/11/2026. Consulte [HOMOLOGACAO-1.0.0.md](HOMOLOGACAO-1.0.0.md) para os resultados e seus limites.

SHA-256 do ZIP:

```text
5BA5006504F68C8A3A4095E4317F130AB5E793F5CE7BC78F94E63AEEDCAA456D
```

## Código-fonte

O código está em [source](source). O repositório reúne as fontes de texto; os binários, as dependências e todos os arquivos originais dos componentes também estão disponíveis no pacote completo da release.

Para preparar uma cópia de desenvolvimento, extraia o pacote completo e copie as fontes deste repositório sobre essa pasta. O script `Compilar.ps1` usa o compilador .NET Framework do Windows. O parâmetro opcional `-GoogleOAuthFile` permite incluir o cadastro de aplicativo de computador na compilação; o arquivo de cadastro e as autorizações pessoais não são versionados neste repositório.

`ARQUIVOS-SHA256.csv` descreve os arquivos do pacote completo da distribuição.

## Licenciamento

O **código próprio do OuO tem uso gratuito e código visível, com reutilização reservada**, conforme a [Licença de Uso Gratuito e Código Visível](LICENSE.md). É permitido usar o programa, inclusive na atividade profissional, e compartilhar o pacote oficial íntegro. Reaproveitar o código original em outros projetos ou criar distribuições modificadas exige autorização, ressalvados os direitos legais e as permissões da plataforma.

Os componentes de terceiros mantêm suas respectivas licenças e atribuições. Os avisos estão em [THIRD-PARTY.md](THIRD-PARTY.md), [licenses](licenses) e [third-party-source](third-party-source), incluindo fontes e instruções dos auxiliares distribuídos.

## Overclock Informática

[Página de links e contato](https://linkme.bio/overclockinformatica) · [Instagram](https://www.instagram.com/oc.informatica/)

## Próxima versão

Os pedidos de 22/11/2026 e seus critérios de conclusão estão em [PROXIMA-VERSAO.md](PROXIMA-VERSAO.md). A licença já foi definida; os demais itens dessa rodada estão planejados para a próxima implementação.
