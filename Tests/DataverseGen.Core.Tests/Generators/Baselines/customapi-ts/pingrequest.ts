export default class PingRequest implements IWebApiRequest {
	private operationName: string;

	constructor() {
		this.operationName = "newping";
	}
	getInternalRequest(): IWebApiInternalRequest {
		return {
			getMetadata: () => ({
				boundParameter: null,
				parameterTypes: {
				},
				operationType: WebApiRequestOperationType.Action,
				operationName: this.operationName
			})
		} as IWebApiInternalRequest;
	}
}
