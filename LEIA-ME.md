# OuO 1.0.0.107 — mídias do OneDrive e correção de saída

Extraia o pacote, feche a versão anterior e abra **OuO.exe**. A conexão salva nas versões anteriores continua disponível neste perfil do Windows.

## Fotos e vídeos no OneDrive

1. Abra **Diagnóstico → Armazenamento em nuvem** e escolha **OneDrive**.
2. Se necessário, use **Conectar OneDrive** e faça o login no navegador. Depois da conexão, a análise começa.
3. Use **Analisar espaço** para atualizar o inventário. Em contas grandes, a leitura completa pode levar vários minutos. A operação e a contagem de itens consultados aparecem durante a leitura.
4. Em **Tipo de arquivo da lista**, escolha **Fotos e imagens** ou **Vídeos**. Os maiores arquivos aparecem primeiro. A busca filtra pelo nome e caminho; as pastas também mostram o tamanho da categoria escolhida.
5. Use **Próxima página** e **Página anterior** para percorrer a lista. Cada página mostra até 100 arquivos. Mudar o filtro ou navegar nos resultados reaproveita os dados em memória.

O tipo usa a informação fornecida pelo serviço; quando ela não está disponível, usa a extensão do arquivo. Extensões ambíguas, como .ts e .mts, só entram como vídeo quando o serviço informa esse tipo; arquivos de declaração de programação ficam em Outros. Fotos e imagens podem incluir imagens de programas, além de fotografias pessoais.

## Entender a diferença de espaço

**De onde vem o uso da conta** separa os tamanhos conhecidos de imagens, vídeos e demais arquivos. A lixeira usa o valor informado pela Microsoft. **Saldo não explicado** é a diferença que ainda não foi atribuída pelos dados consultados.

A cota pessoal da Microsoft pode incluir outros serviços. Itens sem tamanho, pastas sem acesso, compartilhamentos e versões também podem afetar a comparação. A tela informa quando a listagem está parcial ou os totais divergem; nenhum saldo é atribuído automaticamente a uma causa.

**Atualizar cota e lixeira** consulta somente esses totais e preserva a lista de arquivos. É mais rápido que refazer o inventário completo.

Para investigar um arquivo, selecione-o e clique em **Checar histórico do arquivo**. O OuO mostra os metadados das versões que a Microsoft retornar, da mais recente para a mais antiga. O tamanho de uma versão não mede, por si, o espaço adicional cobrado na conta. A consulta não baixa, restaura ou exclui versões.

## Correção do aviso inicial

O botão **Sair** encerra o programa depois que a criação da janela é concluída. A rotina de janela ignora identificadores já encerrados. A recusa no botão Sair, o fechamento do aviso e a recusa no modo de bandeja foram testados sem iniciar o monitoramento de hardware.

## Google e estado da prévia

A divisão do consumo Google entre serviços foi mantida. A lista de arquivos do Google Drive continua separada da biblioteca do Google Fotos. A análise individual do Fotos foi adiada pelo usuário; veja [as alternativas](GOOGLE-FOTOS-OPCOES.md). O saldo Google sem identificação não recebeu uma categoria por suposição.

O analisador consulta os dados em modo de leitura. A compilação permanece sem assinatura, e os recursos com ensaios pendentes continuam identificados. Consulte [Validação](HOMOLOGACAO-1.0.0.md) e [Estado do escopo](STATUS-DO-ESCOPO.md).
